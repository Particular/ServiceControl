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

        public void Apply(IDocumentStore store)
        {
            var currentPage = 0;
            int retrievedResults;

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
                        Check(failedMessage, session);
                    }

                    session.SaveChanges();
                }
            } while (retrievedResults == PageSize);
        }

        private void Check(FailedMessage failedMessage, IDocumentSession session)
        {
            var originalRecords = failedMessage.ProcessingAttempts.Select(x => new ProcessingAttemptRecord(x)).ToArray();
            var retries = originalRecords.Where(x => x.RetryId != null).ToArray();
            var nonRetries = originalRecords.Except(retries).ToArray();

            if (nonRetries.Length == 1)
            {
                return;
            }

            var grouped = nonRetries.GroupBy(x => x.NewUniqueMessageId).ToArray();
            if (grouped.Length == 1)
            {
                return;
            }

            foreach (var group in grouped)
            {
                if (retries.Any(x => x.NewUniqueMessageId == group.Key))
                {
                    // We've retried to this endpoint so it's the primary
                    continue;
                }

                var newFailedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(group.Key),
                    UniqueMessageId = group.Key,
                    ProcessingAttempts = group.Select(x => x.Attempt).OrderBy(x => x.FailureDetails.TimeOfFailure).ToList(),
                    Status = FailedMessageStatus.Unresolved
                };

                var lastProcessingAttempt = newFailedMessage.ProcessingAttempts.Last();

                newFailedMessage.FailureGroups = failedMessageFactory.GetGroups((string)lastProcessingAttempt.MessageMetadata["MessageType"], lastProcessingAttempt.FailureDetails);

                session.Store(newFailedMessage);

                foreach (var attempt in newFailedMessage.ProcessingAttempts)
                {
                    failedMessage.ProcessingAttempts.Remove(attempt);
                }
            }

            if (failedMessage.ProcessingAttempts.Count == 0)
            {
                session.Delete(failedMessage);
            }
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

        public string MigrationId { get; } = "Split Failed Message Documents";

        private const int PageSize = 1024;
        private FailedMessageFactory failedMessageFactory;
    }
}