namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.MessageFailures;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Recoverability;

public class RetryBatchesManager : IRetryBatchesManager
{
    public void Delete(RetryBatch retryBatch) =>
        throw new NotImplementedException();

    public void Delete(RetryBatchNowForwarding forwardingBatch) =>
        throw new NotImplementedException();

    public Task<FailedMessageRetry[]> GetFailedMessageRetries(IList<string> stagingBatchFailureRetries) =>
        throw new NotImplementedException();

    public void Evict(FailedMessageRetry failedMessageRetry) =>
        throw new NotImplementedException();

    public Task<FailedMessage[]> GetFailedMessages(Dictionary<string, FailedMessageRetry>.KeyCollection keys) =>
        throw new NotImplementedException();

    public Task<RetryBatchNowForwarding> GetRetryBatchNowForwarding() =>
        throw new NotImplementedException();

    public Task<RetryBatch> GetRetryBatch(string retryBatchId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<RetryBatch> GetStagingBatch() =>
        throw new NotImplementedException();

    public Task Store(RetryBatchNowForwarding retryBatchNowForwarding) =>
        throw new NotImplementedException();

    public Task<MessageRedirectsCollection> GetOrCreateMessageRedirectsCollection() =>
        throw new NotImplementedException();

    public Task CancelExpiration(FailedMessage failedMessage) =>
        throw new NotImplementedException();

    public Task SaveChanges() =>
        throw new NotImplementedException();

    public void Dispose()
    {
        // Nothing to dispose yet
        GC.SuppressFinalize(this);
    }
}
