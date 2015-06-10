namespace ServiceControl.Groups.Retry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Logging;
    using NServiceBus.Saga;
    using Raven.Client;
    using ServiceControl.Groups.Indexes;
    using ServiceControl.MessageFailures.InternalMessages;

    public class BulkRetryPolicy : Saga<BulkRetryPolicy.BulkRetryPolicyData>, 
        IAmStartedByMessages<RetryAllInGroup>,
        IHandleTimeouts<BulkRetryPolicy.RetryMoreTimeout>
    {
        const int BATCH_SIZE = 1000;
        const int SECONDS_BETWEEN_BATCH = 30;
        const int TERMINATE_AFTER_BATCHES_WITH_NO_ACTIVITY = 4;

        public void Handle(RetryAllInGroup message)
        {
            if (Data.StartedAt.HasValue)
            {
                // There is already a retry in progress
                return;
            }

            Data.GroupId = message.GroupId;
            Data.StartedAt = message.StartedAt;
            Logger.InfoFormat("Bulk Retry Begins: {0}", Data.GroupId);

            int totalCount;
            var candidates = GetRetryableMessages(BATCH_SIZE, out totalCount);
            RetryMessages(candidates);
            IssueCallback(totalCount);
        }

        private void IssueCallback(int totalCount)
        {
            Logger.DebugFormat("Bulk Retry: Next count should not equal {0}", totalCount);
            RequestTimeout<RetryMoreTimeout>(TimeSpan.FromSeconds(SECONDS_BETWEEN_BATCH), m => m.TotalCount = totalCount);
        }

        public void Timeout(RetryMoreTimeout state)
        {
            int totalCount;
            var candidates = GetRetryableMessages(BATCH_SIZE, out totalCount);
            Logger.InfoFormat("Bulk Retry: {0} - {1} remain", Data.GroupId, totalCount);

            if (totalCount != state.TotalCount)
            {
                RetryMessages(candidates);
                Data.UpdatesWithoutChange = 0;
            }
            else
            {
                Data.UpdatesWithoutChange++;
                Logger.InfoFormat("Bulk Retry: {0} - {1} updates without change", Data.GroupId, Data.UpdatesWithoutChange);
            }

            var allDone = totalCount == 0;

            if (allDone || Data.UpdatesWithoutChange >= TERMINATE_AFTER_BATCHES_WITH_NO_ACTIVITY)
            {
                CompleteRetry(allDone);
            }
            else
            {
                IssueCallback(totalCount);
            }
        }

        private void RetryMessages(IEnumerable<string> messageIds)
        {
            foreach(var id in messageIds)
                RetryMessage(id);
        }

        private void RetryMessage(string messageId)
        {
            Logger.DebugFormat("Bulk Retry Message: {0}", messageId);
            Bus.SendLocal<RetryMessage>(m => m.FailedMessageId = messageId);
        }

        private void CompleteRetry(bool allDone = false)
        {
            MarkAsComplete();
            Logger.InfoFormat("Bulk Retry Ended: {0}", Data.GroupId);
            Bus.Publish(new BulkRetryCompleted
            {
                GroupId = Data.GroupId, 
                RanToCompletion = allDone
            });
        }

        private IEnumerable<string> GetRetryableMessages(int number, out int total)
        {
            RavenQueryStatistics stats;

            var results = Session.Query<MessageFailuresByFailureGroupsIndex.StoredFields, MessageFailuresByFailureGroupsIndex>()
                .Customize(q => q.WaitForNonStaleResultsAsOfLastWrite())
                .Statistics(out stats)
                .Where(m => m.FailureGroups_Id == Data.GroupId && m.LastAttempt <= Data.StartedAt)
                .OrderBy(m => m.LastAttempt)
                .Select(x => x.FailedMessageId)
                .Take(number)
                .ToArray();

            total = stats.TotalResults;

            return results;
        }

        public IDocumentSession Session { get; set; }

        public override void ConfigureHowToFindSaga()
        {
            ConfigureMapping<RetryAllInGroup>(m => m.GroupId)
                .ToSaga(s => s.GroupId);
        }

        public class BulkRetryPolicyData : ContainSagaData
        {
            [Unique]
            public string GroupId { get; set; }
            public DateTimeOffset? StartedAt { get; set; }
            public int UpdatesWithoutChange { get; set; }
        }

        public class RetryMoreTimeout
        {
            public int TotalCount { get; set; }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(BulkRetryPolicy));
    }
}
