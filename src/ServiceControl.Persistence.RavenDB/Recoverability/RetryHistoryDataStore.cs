namespace ServiceControl.Persistence.RavenDB.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Recoverability;

    class RetryHistoryDataStore(IRavenSessionProvider sessionProvider) : IRetryHistoryDataStore
    {
        const string DocumentId = "RetryOperations/History";

        public async Task<RetryHistory> GetRetryHistory(CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var retryHistory = await session.LoadAsync<RetryHistory>(DocumentId, cancellationToken);

            retryHistory ??= new();

            return retryHistory;
        }

        public async Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime, DateTime completionTime,
            string originator, string classifier, bool messageFailed, int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth,
            CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var retryHistory = await session.LoadAsync<RetryHistory>(DocumentId, cancellationToken) ?? new();

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

            await session.StoreAsync(retryHistory, DocumentId, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> AcknowledgeRetryGroup(string groupId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var retryHistory = await session.LoadAsync<RetryHistory>(DocumentId, cancellationToken);
            if (retryHistory != null)
            {
                if (retryHistory.Acknowledge(groupId, RetryType.FailureGroup))
                {
                    await session.StoreAsync(retryHistory, DocumentId, cancellationToken);
                    await session.SaveChangesAsync(cancellationToken);

                    return true;
                }
            }

            return false;
        }
    }
}