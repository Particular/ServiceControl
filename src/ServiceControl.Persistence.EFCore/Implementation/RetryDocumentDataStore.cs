namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

public class RetryDocumentDataStore : IRetryDocumentDataStore
{
    public Task StageRetryByUniqueMessageIds(string batchDocumentId, string[] messageIds) =>
        throw new NotImplementedException();

    public Task MoveBatchToStaging(string batchDocumentId) =>
        throw new NotImplementedException();

    public Task<string> CreateBatchDocument(string retrySessionId, string requestId, RetryType retryType,
        string[] failedMessageRetryIds, string originator, DateTime startTime, DateTime? last = null,
        string? batchName = null, string? classifier = null,
        string? initiatedById = null, string? initiatedByName = null, string? operationId = null) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<RetryBatch>>> QueryOrphanedBatches(string retrySessionId) =>
        throw new NotImplementedException();

    public Task<IList<RetryBatchGroup>> QueryAvailableBatches() =>
        throw new NotImplementedException();

    public Task GetBatchesForAll(DateTime cutoff, Func<string, DateTime, Task> callback) =>
        throw new NotImplementedException();

    public Task GetBatchesForEndpoint(DateTime cutoff, string endpoint, Func<string, DateTime, Task> callback) =>
        throw new NotImplementedException();

    public Task GetBatchesForFailedQueueAddress(DateTime cutoff, string failedQueueAddresspoint, FailedMessageStatus status, Func<string, DateTime, Task> callback) =>
        throw new NotImplementedException();

    public Task GetBatchesForFailureGroup(string groupId, string groupTitle, string groupType, DateTime cutoff, Func<string, DateTime, Task> callback) =>
        throw new NotImplementedException();

    public Task<FailureGroupView> QueryFailureGroupViewOnGroupId(string groupId) =>
        throw new NotImplementedException();
}
