namespace Particular.ServiceControl.DbMigrations
{
    using System;
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

        public string Apply(IDocumentStore store)
        {
            var currentPage = 0;

            var stats = new MigrationStats();
            const string tempCollectionName = "FailedMessagesCopy";

            store.Conventions.DefaultQueryingConsistency = ConsistencyOptions.AlwaysWaitForNonStaleResultsAsOfLastWrite;

            using (var session = store.OpenSession())
            {
                var redirects = MessageRedirectsCollection.GetOrCreate(session);

                WaitForNonStaleIndexes(session);

                session.Advanced.DocumentStore.DatabaseCommands.UpdateByIndex(DocumentsByEntityName, new IndexQuery
                {
                    Query = "Tag:FailedMessages"
                }, new ScriptedPatchRequest
                {
                    Script = $@"PutDocument('{tempCollectionName}/' + this.UniqueMessageId, this, {{ ""Raven-Entity-Name"" : ""{tempCollectionName}"", ""Raven-Clr-Type"": ""{typeof(FailedMessage).AssemblyQualifiedName}"" }})"
                });

                WaitForNonStaleIndexes(session);

                session.Advanced.DocumentStore.DatabaseCommands.DeleteByIndex(DocumentsByEntityName, new IndexQuery
                {
                    Query = "Tag:FailedMessages"
                }, true);

                WaitForNonStaleIndexes(session);

                int retrievedResults;
                do
                {
                    var failedMessages = session.Advanced.LoadStartingWith<FailedMessage>(
                        $"{tempCollectionName}/",
                        start: PageSize*currentPage,
                        pageSize: PageSize);

                    currentPage++;

                    retrievedResults = failedMessages.Length;

                    foreach (var failedMessage in failedMessages)
                    {
                        stats += Check(failedMessage, redirects.Redirects, session);
                    }

                    session.SaveChanges();
                } while (retrievedResults == PageSize);

                session.Advanced.DocumentStore.DatabaseCommands.DeleteByIndex(DocumentsByEntityName, new IndexQuery
                {
                    Query = "Tag:OldFailedMessages"
                }, true);

                WaitForNonStaleIndexes(session);
            }

            return $"Found {stats.FoundProblem} issue(s) in {stats.Checked} Failed Message document(s). Created {stats.Created} new document(s). Deleted {stats.Deleted} old document(s).";
        }

        private static void WaitForNonStaleIndexes(IDocumentSession session)
        {
            session.Query<dynamic>(DocumentsByEntityName)
                .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                .ToList();
        }

        private MigrationStats Check(FailedMessage failedMessage, List<MessageRedirect> redirects, IDocumentSession session)
        {
            var stats = new MigrationStats
            {
                Checked = 1
            };

            var processingAttempts = failedMessage.ProcessingAttempts
                .Select((a, i) => new ProcessingAttemptRecord(a, i, redirects))
                .ToArray();

            var failedMessages = processingAttempts.GroupBy(p => p.NewUniqueMessageId).Select(g => new FailedMessage
            {
                Id = FailedMessage.MakeDocumentId(g.Key),
                UniqueMessageId = g.Key,
                ProcessingAttempts = g.OrderBy(a => a.Index).Select(a => a.Attempt).ToList(),
            }).ToList();

            var hasMoreThan1Origin = failedMessages.Count > 1;

            if (hasMoreThan1Origin)
            {
                stats.FoundProblem++;
            }

            failedMessages.ForEach(f =>
            {
                if (!hasMoreThan1Origin || //No split was required
                    failedMessage.Status == FailedMessageStatus.Unresolved || //We cannot determine status and last known was unresolved, so keep it that way
                    ((failedMessage.Status == FailedMessageStatus.RetryIssued || failedMessage.Status == FailedMessageStatus.Archived) && HasSameLast(failedMessage, f))) //Status indicates user action on last attempt and this is the split that has that attempt, so preserve user intent
                {
                    f.Status = failedMessage.Status;
                }

                f.Status = FailedMessageStatus.Unresolved;

                if (HasSameLast(failedMessage, f))
                {
                    f.Status = failedMessage.Status;
                }

                var lastAttempt = f.ProcessingAttempts.Last();

                object messageType;

                if (lastAttempt.MessageMetadata.TryGetValue("MessageType", out messageType))
                {
                    f.FailureGroups = failedMessageFactory.GetGroups((string)messageType, lastAttempt.FailureDetails);
                }

                if (failedMessage.UniqueMessageId != f.UniqueMessageId)
                {
                    stats.Created++;
                }
            });

            if (failedMessages.All(f => f.UniqueMessageId != failedMessage.UniqueMessageId))
            {
                stats.Deleted++;
            }

            failedMessages.ForEach(session.Store);

            return stats;
        }

        static bool HasSameLast(FailedMessage original, FailedMessage failure)
        {
            var originalLast = original.ProcessingAttempts.Last();
            var failureLast = failure.ProcessingAttempts.Last();

            return originalLast.FailureDetails.AddressOfFailingEndpoint == failureLast.FailureDetails.AddressOfFailingEndpoint;
        }

        class ProcessingAttemptRecord
        {
            public ProcessingAttemptRecord(FailedMessage.ProcessingAttempt attempt, int i, IEnumerable<MessageRedirect> redirects)
            {
                Attempt = attempt;
                Index = i;

                var headers = new Dictionary<string, string>(attempt.Headers);

                if (!headers.ContainsKey(Headers.ProcessingEndpoint))
                {
                    var address = attempt.FailureDetails.AddressOfFailingEndpoint;

                    var redirect = redirects.SingleOrDefault(r => r.ToPhysicalAddress == address);

                    headers.Add(Headers.ProcessingEndpoint, redirect != null ? redirect.FromPhysicalAddress : address);
                }

                NewUniqueMessageId = headers.UniqueId();
            }

            public FailedMessage.ProcessingAttempt Attempt { get; }
            public string NewUniqueMessageId { get; }
            public int Index { get; }
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