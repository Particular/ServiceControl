namespace Particular.ServiceControl.DbMigrations
{
    using System.Linq;
    using global::ServiceControl;
    using global::ServiceControl.MessageFailures;
    using global::ServiceControl.Operations;
    using global::ServiceControl.Recoverability;
    using NServiceBus.ObjectBuilder;
    using Raven.Client;

    public class SplitFailedMessageDocumentsMigration : IMigration
    {
        public SplitFailedMessageDocumentsMigration(IBuilder builder)
        {
            var failedEnrichers = builder.BuildAll<IFailedMessageEnricher>().ToArray();

            failedMessageFactory = new FailedMessageFactory(failedEnrichers);
        }

        public string Apply(IDocumentStore store)
        {
            var currentPage = 0;
            int retrievedResults;

            var stats = new MigrationStats();

            do
            {
                using (var session = store.OpenSession())
                {

                    var failedMessages = session.Advanced.LoadStartingWith<FailedMessage>(
                        FailedMessage.MakeDocumentId(string.Empty),
                        start: PageSize*currentPage,
                        pageSize: PageSize);

                    currentPage++;

                    retrievedResults = failedMessages.Length;

                    foreach (var failedMessage in failedMessages)
                    {
                        stats += Check(failedMessage, session);
                    }

                    session.SaveChanges();
                }
            } while (retrievedResults == PageSize);

            return $"Found {stats.FoundProblem} issue(s) in {stats.Checked} Failed Message document(s). Created {stats.Created} new document(s). Deleted {stats.Deleted} old document(s).";
        }

        private MigrationStats Check(FailedMessage failedMessage, IDocumentSession session)
        {
            var stats = new MigrationStats
            {
                Checked = 1
            };

            var retryAttempts = failedMessage.ProcessingAttempts
                .Select(x => new ProcessingAttemptRecord(x))
                .Reverse().ToArray();

            ProcessingAttemptRecord[] collapsedRetries;

            //If there is any failed message processing initiated from SC of any in-flight
            if (retryAttempts.First().RetryId != null || failedMessage.Status == FailedMessageStatus.RetryIssued)
            {
                //Keep all SC-initiated failures + initial one
                //TODO: check if skip one can throw
                collapsedRetries = retryAttempts
                    .SkipWhile(ra => ra.RetryId != null)
                    .Skip(1).ToArray();
            }
            else
            {
                //Split all retry attempts from the original document
                collapsedRetries = retryAttempts;
            }

            if (collapsedRetries.Any())
            {
                stats.FoundProblem = 1;
            }

            foreach (var collapsedRetry in collapsedRetries)
            {
                var newFailedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(collapsedRetry.NewUniqueMessageId),
                    UniqueMessageId = collapsedRetry.NewUniqueMessageId,
                    ProcessingAttempts = new[]
                    {
                        collapsedRetry.Attempt
                    }.ToList(),
                    Status = FailedMessageStatus.Unresolved
                };

                stats.Created += 1;

                session.Store(newFailedMessage);

                failedMessage.ProcessingAttempts.Remove(collapsedRetry.Attempt);
            }

            if (failedMessage.ProcessingAttempts.Count == 0)
            {
                session.Delete(failedMessage);
                stats.Deleted = 1;
            }

            return stats;
        }

        class ProcessingAttemptRecord
        {
            public ProcessingAttemptRecord(FailedMessage.ProcessingAttempt attempt)
            {
                Attempt = attempt;

                string uniqueMessageId;
                if (attempt.Headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out uniqueMessageId))
                {
                    RetryId = uniqueMessageId;
                }

                NewUniqueMessageId = attempt.Headers.UniqueId();
            }

            public FailedMessage.ProcessingAttempt Attempt { get; }
            public string RetryId { get; }
            public string NewUniqueMessageId { get;  }
        }

        struct MigrationStats
        {
            public int Checked { get; set; }
            public int FoundProblem { get; set; }
            public int Created { get; set; }
            public int Deleted { get; set; }

            public static MigrationStats operator +(MigrationStats left, MigrationStats right)
                => new MigrationStats
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