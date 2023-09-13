namespace ServiceControl.Persistence.RavenDb.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Recoverability;
    using RavenDb5;

    class RetryHistoryDataStore : IRetryHistoryDataStore
    {
        public RetryHistoryDataStore(DocumentStoreProvider storeProvider)
        {
            this.storeProvider = storeProvider;
        }

        public async Task<RetryHistory> GetRetryHistory()
        {
            using var session = storeProvider.Store.OpenAsyncSession();
            var id = RetryHistory.MakeId();
            var retryHistory = await session.LoadAsync<RetryHistory>(id);

            retryHistory ??= RetryHistory.CreateNew();

            return retryHistory;
        }

        public async Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime, DateTime completionTime,
            string originator, string classifier, bool messageFailed, int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth)
        {
            using var session = storeProvider.Store.OpenAsyncSession();
            var retryHistory = await session.LoadAsync<RetryHistory>(RetryHistory.MakeId()) ?? RetryHistory.CreateNew();

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

            await session.StoreAsync(retryHistory);
            await session.SaveChangesAsync();
        }

        public async Task<bool> AcknowledgeRetryGroup(string groupId)
        {
            using var session = storeProvider.Store.OpenAsyncSession();
            var retryHistory = await session.LoadAsync<RetryHistory>(RetryHistory.MakeId());
            if (retryHistory != null)
            {
                if (retryHistory.Acknowledge(groupId, RetryType.FailureGroup))
                {
                    await session.StoreAsync(retryHistory);
                    await session.SaveChangesAsync();

                    return true;
                }
            }

            return false;
        }

        readonly DocumentStoreProvider storeProvider;
    }
}