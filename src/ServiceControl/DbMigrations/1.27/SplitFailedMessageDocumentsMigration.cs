namespace Particular.ServiceControl.DbMigrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::ServiceControl;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.MessageFailures;
    using global::ServiceControl.MessageRedirects;
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

            using (var session = store.OpenSession())
            {
                int retrievedResults;
                int currentPage = 0;

                do
                {
                    var failedMessages = session.Advanced.LoadStartingWith<FailedMessage>(
                        $"FailedMessages/",
                        start: PageSize*currentPage++,
                        pageSize: PageSize);

                    foreach (var failedMessage in failedMessages)
                    {
                        stats += MigrateFromTemporaryCollection(failedMessage, session);
                    }

                    session.SaveChanges();

                    retrievedResults = failedMessages.Length;

                } while (retrievedResults == PageSize);
            }

            return $"Found {stats.FoundProblem} issue(s) in {stats.Checked} Failed Message document(s). Created {stats.Created} new document(s). Deleted {stats.Deleted} old document(s).";
        }

        private MigrationStats MigrateFromTemporaryCollection(FailedMessage originalFailedMessage, IDocumentSession session)
        {
            var stats = new MigrationStats { Checked = 1 };

            var processingAttempts = originalFailedMessage.ProcessingAttempts
                .Select((a, i) => new ProcessingAttemptRecord(a, i))
                .ToArray();

            //Split the original FailedMessage into separate documents based on new unique message id
            var newFailedMessages = processingAttempts
                .GroupBy(p => p.UniqueMessageId)
                .Select(g => new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(g.Key),
                    UniqueMessageId = g.Key,
                    ProcessingAttempts = g.OrderBy(a => a.Index).Select(a => a.Attempt).ToList(),
                    Status = FailedMessageStatus.Unresolved
                }).ToList();

            //Do nothing if we don't split the document
            if (newFailedMessages.Count == 1) return stats;

            stats.FoundProblem++;

            session.Delete(originalFailedMessage);
            stats.Deleted++;

            newFailedMessages.ForEach(newFailedMessage =>
            {
                var lastAttempt = newFailedMessage.ProcessingAttempts.Last();

                object messageType;

                if (lastAttempt.MessageMetadata.TryGetValue("MessageType", out messageType))
                {
                    newFailedMessage.FailureGroups = failedMessageFactory.GetGroups((string)messageType, lastAttempt.FailureDetails);
                    var splitGroup = CreateSplitFailureGroup(lastAttempt, messageType, originalFailedMessage.Status);
                    if (splitGroup != null)
                    {
                        newFailedMessage.FailureGroups.Add(splitGroup);
                    }
                }

                session.Store(newFailedMessage);
                stats.Created++;
            });

            return stats;
        }

        public static FailedMessage.FailureGroup CreateSplitFailureGroup(FailedMessage.ProcessingAttempt attempt, object messageType, FailedMessageStatus status)
        {
            var endpointName = attempt.Headers.ContainsKey(Headers.OriginatingEndpoint) ? attempt.Headers[Headers.OriginatingEndpoint] : attempt.FailureDetails.AddressOfFailingEndpoint;
            var classification = $"{endpointName}/{messageType}/{status}";
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