namespace Particular.ServiceControl.DbMigrations
{
    using System.Collections.Generic;
    using System.Linq;
    using global::ServiceControl;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.MessageFailures;
    using global::ServiceControl.Operations;
    using global::ServiceControl.Recoverability;
    using NServiceBus;
    using NServiceBus.ObjectBuilder;
    using Raven.Client;
    using Raven.Client.Document;

    public class SplitFailedMessageDocumentsMigration : IMigration
    {
        public SplitFailedMessageDocumentsMigration(IBuilder builder)
        {
            var failedEnrichers = builder.BuildAll<IFailedMessageEnricher>().ToArray();

            failedMessageFactory = new FailedMessageFactory(failedEnrichers);
        }

        public string Apply(IDocumentStore store)
        {
            store.Conventions.DefaultQueryingConsistency = ConsistencyOptions.AlwaysWaitForNonStaleResultsAsOfLastWrite;

            var stats = new MigrationStats();

            int retrievedResults;
            var currentPage = 0;

            do
            {
                using (var session = store.OpenSession())
                {
                    var failedMessages = session.Advanced.LoadStartingWith<FailedMessage>(
                        $"FailedMessages/",
                        start: PageSize * currentPage++,
                        pageSize: PageSize);

                    foreach (var failedMessage in failedMessages)
                    {
                        stats += MigrateFromTemporaryCollection(failedMessage, session);
                    }

                    session.SaveChanges();

                    retrievedResults = failedMessages.Length;
                }
            } while (retrievedResults == PageSize);

            return $"Found {stats.FoundProblem} issue(s) in {stats.Checked} Failed Message document(s). Created {stats.Created} new document(s). Deleted {stats.Deleted} old document(s).";
        }

        private MigrationStats MigrateFromTemporaryCollection(FailedMessage originalFailedMessage, IDocumentSession session)
        {
            var originalStatus = originalFailedMessage.Status;

            var stats = new MigrationStats { Checked = 1 };

            var processingAttempts = originalFailedMessage.ProcessingAttempts
                .Select((a, i) => new ProcessingAttemptRecord(a, i))
                .ToArray();

            //Split the original FailedMessage into separate documents based on new unique message id
            var failedMessages = processingAttempts
                .GroupBy(p => p.UniqueMessageId)
                .Select(g => new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(g.Key),
                    UniqueMessageId = g.Key,
                    ProcessingAttempts = g.OrderBy(a => a.Index).Select(a => a.Attempt).ToList(),
                    Status = FailedMessageStatus.Unresolved
                }).ToList();

            //Do nothing if we don't split the document
            if (failedMessages.Count == 1) return stats;

            stats.FoundProblem++;

            if (failedMessages.All(f => f.UniqueMessageId != originalFailedMessage.UniqueMessageId))
            {
                session.Delete(originalFailedMessage);
                stats.Deleted++;
            }
            else
            {
                var failedMessageCopy = failedMessages.Single(f => f.UniqueMessageId == originalFailedMessage.UniqueMessageId);
                failedMessages.Remove(failedMessageCopy);
                failedMessages.Add(originalFailedMessage);

                originalFailedMessage.ProcessingAttempts = failedMessageCopy.ProcessingAttempts;
                originalFailedMessage.Status = failedMessageCopy.Status;
            }

            failedMessages.ForEach(failedMessage =>
            {
                var lastAttempt = failedMessage.ProcessingAttempts.Last();

                object messageType;

                if (lastAttempt.MessageMetadata.TryGetValue("MessageType", out messageType))
                {
                    if (!failedMessage.FailureGroups.Any())
                    {
                        failedMessage.FailureGroups = failedMessageFactory.GetGroups((string)messageType, lastAttempt.FailureDetails);
                    }
                    failedMessage.FailureGroups.Add(CreateSplitFailureGroup(lastAttempt, messageType, originalStatus));
                }

                if (failedMessage.UniqueMessageId == originalFailedMessage.UniqueMessageId) return;

                session.Store(failedMessage);
                stats.Created++;
            });

            return stats;
        }

        public static FailedMessage.FailureGroup CreateSplitFailureGroup(FailedMessage.ProcessingAttempt attempt, object messageType, FailedMessageStatus orignalStatus)
        {

            var endpointName = attempt.Headers.ContainsKey(Headers.OriginatingEndpoint) ? attempt.Headers[Headers.OriginatingEndpoint] : attempt.FailureDetails.AddressOfFailingEndpoint;
            var classification = $"{endpointName}/{messageType}/{orignalStatus}";
            const string classifierName = "Split Failure";
            return new FailedMessage.FailureGroup
            {
                Id = DeterministicGuid.MakeId(classifierName, classification).ToString(),
                Title = classification,
                Type = classifierName
            };
        }

        class ProcessingAttemptRecord
        {
            public ProcessingAttemptRecord(FailedMessage.ProcessingAttempt attempt, int index)
            {
                Attempt = attempt;
                Index = index;

                var headers = new Dictionary<string, string>(attempt.Headers);

                UniqueMessageId = headers.UniqueId();

                IsRetry = headers.ContainsKey("ServiceControl.Retry.UniqueMessageId");
            }

            public FailedMessage.ProcessingAttempt Attempt { get; }
            public string UniqueMessageId { get; }
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

        public const int PageSize = 1024;
        private FailedMessageFactory failedMessageFactory;
    }
}