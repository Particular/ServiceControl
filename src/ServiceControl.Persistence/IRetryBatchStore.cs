namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using ServiceControl.MessageFailures;

    public interface IRetryBatchStore
    {
        Task<string> CreateBatch(string retrySessionId, string requestId, RetryType retryType,
            string[] failedMessageRetryIds, string originator, DateTime startTime, DateTime? last = null,
            string batchName = null, string classifier = null,
            string initiatedById = null, string initiatedByName = null, string operationId = null);

        Task AssignMessagesToBatch(string batchId, string[] messageIds);

        Task MoveBatchToStaging(string batchId);

        Task<QueryResult<IList<RetryBatch>>> GetOrphanedBatches(string retrySessionId);
        Task<IList<RetryBatchGroup>> GetAvailableBatchGroups();

        Task<ForwardingRetryBatch> GetCurrentForwardingBatch();

        Task ForEachUnresolvedMessage(Func<string, DateTime, Task> callback);
        Task ForEachUnresolvedMessageForEndpoint(string endpoint, Func<string, DateTime, Task> callback);
        Task ForEachMessageForQueueAddress(string failedQueueAddress, FailedMessageStatus status, Func<string, DateTime, Task> callback);
        Task ForEachUnresolvedMessageInGroup(string groupId, Func<string, DateTime, Task> callback);
    }
}
