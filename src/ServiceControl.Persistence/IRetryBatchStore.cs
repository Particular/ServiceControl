namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using ServiceControl.MessageFailures;

    public interface IRetryBatchStore
    {
        Task<string> CreateBatch(string retrySessionId, string requestId, RetryType retryType,
            string[] failedMessageRetryIds, string? originator, DateTime startTime, DateTime? last = null,
            string? batchName = null, string? classifier = null,
            string? initiatedById = null, string? initiatedByName = null, string? operationId = null,
            CancellationToken cancellationToken = default);

        Task AssignMessagesToBatch(string batchId, string[] messageIds, CancellationToken cancellationToken = default);

        Task MoveBatchToStaging(string batchId, CancellationToken cancellationToken = default);

        Task<QueryResult<IList<RetryBatch>>> GetOrphanedBatches(string retrySessionId, CancellationToken cancellationToken = default);
        Task<IList<RetryBatchGroup>> GetAvailableBatchGroups(CancellationToken cancellationToken = default);

        Task<ForwardingRetryBatch?> GetCurrentForwardingBatch(CancellationToken cancellationToken = default);

        Task ForEachUnresolvedMessage(Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default);
        Task ForEachUnresolvedMessageForEndpoint(string endpoint, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default);
        Task ForEachMessageForQueueAddress(string failedQueueAddress, FailedMessageStatus status, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default);
        Task ForEachUnresolvedMessageInGroup(string groupId, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default);
    }
}
