namespace ServiceControl.Persistence.Sql;

using System;
using System.Threading.Tasks;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;

class NoOpRetryHistoryDataStore : IRetryHistoryDataStore
{
    public Task<RetryHistory> GetRetryHistory() =>
        Task.FromResult<RetryHistory>(null);

    public Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime,
        DateTime completionTime, string originator, string classifier, bool messageFailed,
        int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth) => Task.CompletedTask;

    public Task<bool> AcknowledgeRetryGroup(string groupId) => Task.FromResult(false);
}
