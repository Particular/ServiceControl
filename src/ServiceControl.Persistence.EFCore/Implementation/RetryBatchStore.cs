namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

public class RetryBatchStore : IRetryBatchStore
{
    public Task<string> CreateBatch(string retrySessionId, string requestId, RetryType retryType,
        string[] failedMessageRetryIds, string originator, DateTime startTime, DateTime? last = null,
        string? batchName = null, string? classifier = null,
        string? initiatedById = null, string? initiatedByName = null, string? operationId = null) =>
        throw new NotImplementedException();

    public Task AssignMessagesToBatch(string batchId, string[] messageIds) =>
        throw new NotImplementedException();

    public Task MoveBatchToStaging(string batchId) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<RetryBatch>>> GetOrphanedBatches(string retrySessionId) =>
        throw new NotImplementedException();

    public Task<IList<RetryBatchGroup>> GetAvailableBatchGroups() =>
        throw new NotImplementedException();

    public Task<ForwardingRetryBatch> GetCurrentForwardingBatch() =>
        throw new NotImplementedException();

    public Task ForEachUnresolvedMessage(Func<string, DateTime, Task> callback) =>
        throw new NotImplementedException();

    public Task ForEachUnresolvedMessageForEndpoint(string endpoint, Func<string, DateTime, Task> callback) =>
        throw new NotImplementedException();

    public Task ForEachMessageForQueueAddress(string failedQueueAddress, FailedMessageStatus status, Func<string, DateTime, Task> callback) =>
        throw new NotImplementedException();

    public Task ForEachUnresolvedMessageInGroup(string groupId, Func<string, DateTime, Task> callback) =>
        throw new NotImplementedException();
}
