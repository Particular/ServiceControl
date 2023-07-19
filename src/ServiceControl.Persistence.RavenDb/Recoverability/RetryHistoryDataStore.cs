namespace ServiceControl.Persistence.RavenDb.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Recoverability;

    class RetryHistoryDataStore : IRetryHistoryDataStore
    {
        public RetryHistoryDataStore(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public async Task<RetryHistory> GetRetryHistory()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var id = RetryHistory.MakeId();
                var retryHistory = await session.LoadAsync<RetryHistory>(id)
                    .ConfigureAwait(false);

                retryHistory = retryHistory ?? RetryHistory.CreateNew();

                return retryHistory;
            }
        }

        public async Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime, DateTime completionTime,
            string originator, string classifier, bool messageFailed, int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var retryHistory = await session.LoadAsync<RetryHistory>(RetryHistory.MakeId()).ConfigureAwait(false) ??
                                   RetryHistory.CreateNew();

                retryHistory.AddToUnacknowledged(new UnacknowledgedRetryOperation
                {
                    RequestId = requestId,
                    RetryType = retryType,
                    StartTime = startTime,
                    CompletionTime = completionTime,
                    Originator = originator,
                    Classifier = classifier,
                    Failed = messageFailed,
                    NumberOfMessagesProcessed = numberOfMessagesProcessed,
                    Last = lastProcessed
                });

                retryHistory.AddToHistory(new HistoricRetryOperation
                {
                    RequestId = requestId,
                    RetryType = retryType,
                    StartTime = startTime,
                    CompletionTime = completionTime,
                    Originator = originator,
                    Failed = messageFailed,
                    NumberOfMessagesProcessed = numberOfMessagesProcessed
                }, retryHistoryDepth);

                await session.StoreAsync(retryHistory)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        public async Task<bool> AcknowledgeRetryGroup(string groupId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var retryHistory = await session.LoadAsync<RetryHistory>(RetryHistory.MakeId()).ConfigureAwait(false);
                if (retryHistory != null)
                {
                    if (retryHistory.Acknowledge(groupId, RetryType.FailureGroup))
                    {
                        await session.StoreAsync(retryHistory).ConfigureAwait(false);
                        await session.SaveChangesAsync().ConfigureAwait(false);

                        return true;
                    }
                }
            }

            return false;
        }

        readonly IDocumentStore documentStore;
    }
}
