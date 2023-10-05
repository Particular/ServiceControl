namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageFailures;
    using MessageRedirects;
    using ServiceControl.Recoverability;

    public interface IRetryBatchesManager : IDataSessionManager
    {
        void Delete(RetryBatch retryBatch);
        void Delete(RetryBatchNowForwarding forwardingBatch);
        Task<FailedMessageRetry[]> GetFailedMessageRetries(IList<string> stagingBatchFailureRetries);
        void Evict(FailedMessageRetry failedMessageRetry);
        Task<FailedMessage[]> GetFailedMessages(Dictionary<string, FailedMessageRetry>.KeyCollection keys);
        Task<RetryBatchNowForwarding> GetRetryBatchNowForwarding();
        Task<RetryBatch> GetRetryBatch(string retryBatchId, CancellationToken cancellationToken);
        Task<RetryBatch> GetStagingBatch();
        Task Store(RetryBatchNowForwarding retryBatchNowForwarding);
        Task<MessageRedirectsCollection> GetOrCreateMessageRedirectsCollection();
        Task CancelExpiration(FailedMessage failedMessage);
    }
}