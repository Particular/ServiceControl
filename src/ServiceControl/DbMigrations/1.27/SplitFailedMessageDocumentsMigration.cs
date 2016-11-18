namespace Particular.ServiceControl.DbMigrations
{
    using System.Collections.Generic;
    using System.Linq;
    using global::ServiceControl;
    using global::ServiceControl.MessageFailures;
    using global::ServiceControl.MessageRedirects;
    using global::ServiceControl.Operations;
    using global::ServiceControl.Recoverability;
    using NServiceBus;
    using NServiceBus.ObjectBuilder;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Document;

    public class SplitFailedMessageDocumentsMigration : IMigration
    {
        public SplitFailedMessageDocumentsMigration(IBuilder builder)
        {
            var failedEnrichers = builder.BuildAll<IFailedMessageEnricher>().ToArray();

            failedMessageFactory = new FailedMessageFactory(failedEnrichers);
        }

        const string DocumentsByEntityName = "Raven/DocumentsByEntityName";
        const string TemporaryCollectionName = "FailedMessagesCopy";

        public string Apply(IDocumentStore store)
        {
            store.Conventions.DefaultQueryingConsistency = ConsistencyOptions.AlwaysWaitForNonStaleResultsAsOfLastWrite;

            var stats = new MigrationStats();

            using (var session = store.OpenSession())
            {
                MoveFailedMessagesToTemporaryCollection(session);

                var redirects = MessageRedirectsCollection.GetOrCreate(session);

                int retrievedResults;
                int currentPage = 0;

                do
                {
                    var failedMessages = session.Advanced.LoadStartingWith<FailedMessage>(
                        $"{TemporaryCollectionName}/",
                        start: PageSize*currentPage,
                        pageSize: PageSize);

                    currentPage++;

                    retrievedResults = failedMessages.Length;

                    foreach (var failedMessage in failedMessages)
                    {
                        stats += MigrateFromTemporaryCollection(failedMessage, redirects.Redirects, session);
                    }

                    session.SaveChanges();

                } while (retrievedResults == PageSize);

                DeleteFailedMessagesFromTemporaryCollection(session);
            }

            return $"Found {stats.FoundProblem} issue(s) in {stats.Checked} Failed Message document(s). Created {stats.Created} new document(s). Deleted {stats.Deleted} old document(s).";
        }

        static void DeleteFailedMessagesFromTemporaryCollection(IDocumentSession session)
        {
            session.Advanced.DocumentStore.DatabaseCommands.DeleteByIndex(DocumentsByEntityName, new IndexQuery
            {
                Query = $"Tag:{TemporaryCollectionName}"
            }, true);

            WaitForNonStaleIndexes(session);
        }

        static void MoveFailedMessagesToTemporaryCollection(IDocumentSession session)
        {
            WaitForNonStaleIndexes(session);

            session.Advanced.DocumentStore.DatabaseCommands.UpdateByIndex(DocumentsByEntityName, new IndexQuery
            {
                Query = "Tag:FailedMessages"
            }, new ScriptedPatchRequest
            {
                Script = $@"PutDocument('{TemporaryCollectionName}/' + this.UniqueMessageId, this, {{ ""Raven-Entity-Name"" : ""{TemporaryCollectionName}"", ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}"" }})"
            });

            WaitForNonStaleIndexes(session);

            session.Advanced.DocumentStore.DatabaseCommands.DeleteByIndex(DocumentsByEntityName, new IndexQuery
            {
                Query = "Tag:FailedMessages"
            }, true);

            WaitForNonStaleIndexes(session);
        }

        private static void WaitForNonStaleIndexes(IDocumentSession session)
        {
            session.Query<dynamic>(DocumentsByEntityName)
                .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                .ToList();
        }

        private MigrationStats MigrateFromTemporaryCollection(FailedMessage originalFailedMessage, List<MessageRedirect> redirects, IDocumentSession session)
        {
            var stats = new MigrationStats { Checked = 1 };

            var processingAttempts = originalFailedMessage.ProcessingAttempts
                .Select((a, i) => new ProcessingAttemptRecord(a, i, redirects))
                .ToArray();

            //When FailedMessage has only one attempt we bring it back unchanged
            if (processingAttempts.Count(pa => pa.IsRetry == false) == 1)
            {
                session.Store(new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(originalFailedMessage.UniqueMessageId),
                    FailureGroups = originalFailedMessage.FailureGroups,
                    ProcessingAttempts = originalFailedMessage.ProcessingAttempts,
                    Status = originalFailedMessage.Status,
                    UniqueMessageId = originalFailedMessage.UniqueMessageId
                });

                return stats;
            }

            stats.FoundProblem++;
            stats.Deleted++;

            //Split the original FailedMessage into separate documents based on new unique message id
            var newFailedMessages = processingAttempts
                .GroupBy(p => p.NewUniqueMessageId)
                .Select(g => new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(g.Key),
                    UniqueMessageId = g.Key,
                    ProcessingAttempts = g.OrderBy(a => a.Index).Select(a => a.Attempt).ToList(),
                }).ToList();


            newFailedMessages.ForEach(newFailedMessage =>
            {
                //QUESTION: the RetryIssued case is true only if we don't have scenario when we split the documents before while send-out did not finish yet
                if (HaveTheSameLastAttempt(originalFailedMessage, newFailedMessage) &&
                    (originalFailedMessage.Status == FailedMessageStatus.Archived || originalFailedMessage.Status == FailedMessageStatus.RetryIssued ))
                {
                    newFailedMessage.Status = originalFailedMessage.Status;
                }
                else
                {
                    newFailedMessage.Status = FailedMessageStatus.Unresolved;
                }

                var lastAttempt = newFailedMessage.ProcessingAttempts.Last();

                object messageType;

                if (lastAttempt.MessageMetadata.TryGetValue("MessageType", out messageType))
                {
                    newFailedMessage.FailureGroups = failedMessageFactory.GetGroups((string)messageType, lastAttempt.FailureDetails);
                }

                session.Store(newFailedMessages);
            });

            //Update stats
            if (newFailedMessages.All(f => f.UniqueMessageId != originalFailedMessage.UniqueMessageId))
            {
                stats.Deleted++;
                stats.Created += newFailedMessages.Count;
            }
            else
            {
                stats.Created += newFailedMessages.Count - 1;
            }

            return stats;
        }

        static bool HaveTheSameLastAttempt(FailedMessage original, FailedMessage failure)
        {
            var originalLast = original.ProcessingAttempts.Last();
            var failureLast = failure.ProcessingAttempts.Last();

            return originalLast.FailureDetails.AddressOfFailingEndpoint == failureLast.FailureDetails.AddressOfFailingEndpoint;
        }

        class ProcessingAttemptRecord
        {
            public ProcessingAttemptRecord(FailedMessage.ProcessingAttempt attempt, int index, IEnumerable<MessageRedirect> redirects)
            {
                Attempt = attempt;
                Index = index;

                var headers = new Dictionary<string, string>(attempt.Headers);

                if (!headers.ContainsKey(Headers.ProcessingEndpoint))
                {
                    var address = attempt.FailureDetails.AddressOfFailingEndpoint;

                    var redirect = redirects.SingleOrDefault(r => r.ToPhysicalAddress == address);

                    headers.Add(Headers.ProcessingEndpoint, redirect != null ? redirect.FromPhysicalAddress : address);
                }

                NewUniqueMessageId = headers.UniqueId();

                IsRetry = headers.ContainsKey("ServiceControl.Retry.UniqueMessageId");
            }

            public FailedMessage.ProcessingAttempt Attempt { get; }
            public string NewUniqueMessageId { get; }
            public int Index { get; }
            public bool IsRetry { get; }
        }

        struct MigrationStats
        {
            public int Checked { get; set; }
            public int FoundProblem { get; set; }
            public int Created { get; set; }
            public int Deleted { get; set; }

            public static MigrationStats operator +(MigrationStats left, MigrationStats right) => new MigrationStats
            {
                Checked = left.Checked + right.Checked,
                Created = left.Created + right.Created,
                FoundProblem = left.FoundProblem + right.FoundProblem,
                Deleted = left.Deleted + right.Deleted
            };
        }

        public string MigrationId { get; } = "Split Failed Message Documents";

        private const int PageSize = 1024;
        private FailedMessageFactory failedMessageFactory;
    }
}