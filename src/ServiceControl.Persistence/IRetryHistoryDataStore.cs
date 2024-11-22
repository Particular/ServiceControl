namespace ServiceControl.Persistence
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Recoverability;

    public interface IRetryHistoryDataStore
    {
        Task<RetryHistory> GetRetryHistory();
        Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime, DateTime completionTime,
            string originator, string classifier, bool messageFailed, int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth);
        Task<bool> AcknowledgeRetryGroup(string groupId);
    }
}