namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Recoverability;

public class RetryHistoryDataStore : IRetryHistoryDataStore
{
    public Task<RetryHistory> GetRetryHistory() =>
        throw new NotImplementedException();

    public Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime, DateTime completionTime,
        string originator, string classifier, bool messageFailed, int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth) =>
        throw new NotImplementedException();

    public Task<bool> AcknowledgeRetryGroup(string groupId) =>
        throw new NotImplementedException();
}
