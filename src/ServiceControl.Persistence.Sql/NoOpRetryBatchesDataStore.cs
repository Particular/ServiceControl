namespace ServiceControl.Persistence.Sql;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Recoverability;

class NoOpRetryBatchesDataStore : IRetryBatchesDataStore
{
    public Task<IRetryBatchesManager> CreateRetryBatchesManager() =>
        Task.FromResult<IRetryBatchesManager>(new NoOpRetryBatchesManager());

    public Task RecordFailedStagingAttempt(IReadOnlyCollection<FailedMessage> messages,
        IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById, Exception e, int maxStagingAttempts,
        string stagingId) => Task.CompletedTask;

    public Task IncrementAttemptCounter(FailedMessageRetry failedMessageRetry) => Task.CompletedTask;

    public Task DeleteFailedMessageRetry(string makeDocumentId) => Task.CompletedTask;

    class NoOpRetryBatchesManager : IRetryBatchesManager
    {
        public void Dispose()
        {
        }

        public Task<RetryBatch> GetBatch(string batchDocumentId) =>
            Task.FromResult<RetryBatch>(null);

        public Task<QueryResult<IList<RetryBatch>>> GetBatches(string status, PagingInfo pagingInfo,
            SortInfo sortInfo) =>
            Task.FromResult(new QueryResult<IList<RetryBatch>>([], QueryStatsInfo.Zero));

        public Task MarkBatchAsReadyForForwarding(string batchDocumentId) => Task.CompletedTask;

        public Task MarkMessageAsSuccessfullyForwarded(FailedMessageRetry messageRetryMetadata, string batchDocumentId) =>
            Task.CompletedTask;

        public Task MarkMessageAsPartOfBatch(string batchId, string uniqueMessageId, FailedMessageStatus status) =>
            Task.CompletedTask;

        public Task AbandonBatch(string batchDocumentId) => Task.CompletedTask;

        public void Delete(RetryBatch retryBatch)
        {
        }

        public void Delete(RetryBatchNowForwarding forwardingBatch)
        {
        }

        public Task<FailedMessageRetry[]> GetFailedMessageRetries(IList<string> stagingBatchFailureRetries) =>
            Task.FromResult(Array.Empty<FailedMessageRetry>());

        public void Evict(FailedMessageRetry failedMessageRetry)
        {
        }

        public Task<FailedMessage[]> GetFailedMessages(Dictionary<string, FailedMessageRetry>.KeyCollection keys) =>
            Task.FromResult(Array.Empty<FailedMessage>());

        public Task<RetryBatchNowForwarding> GetRetryBatchNowForwarding() =>
            Task.FromResult<RetryBatchNowForwarding>(null);

        public Task<RetryBatch> GetRetryBatch(string retryBatchId, CancellationToken cancellationToken) =>
            Task.FromResult<RetryBatch>(null);

        public Task<RetryBatch> GetStagingBatch() =>
            Task.FromResult<RetryBatch>(null);

        public Task Store(RetryBatchNowForwarding retryBatchNowForwarding) => Task.CompletedTask;

        public Task<MessageRedirectsCollection> GetOrCreateMessageRedirectsCollection() =>
            Task.FromResult(new MessageRedirectsCollection());

        public Task CancelExpiration(FailedMessage failedMessage) => Task.CompletedTask;

        public Task SaveChanges() => Task.CompletedTask;
    }
}
