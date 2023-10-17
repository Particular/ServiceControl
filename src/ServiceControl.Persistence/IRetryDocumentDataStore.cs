namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;

    public interface IRetryDocumentDataStore
    {
        Task StageRetryByUniqueMessageIds(string batchDocumentId, string[] messageIds);

        Task MoveBatchToStaging(string batchDocumentId);

        Task<string> CreateBatchDocument(string retrySessionId, string requestId, RetryType retryType,
            string[] failedMessageRetryIds, string originator, DateTime startTime, DateTime? last = null,
            string batchName = null, string classifier = null);

        Task<QueryResult<IList<RetryBatch>>> QueryOrphanedBatches(string retrySessionId);
        Task<IList<RetryBatchGroup>> QueryAvailableBatches();

        // RetriesGateway
        Task GetBatchesForAll(DateTime cutoff, Func<string, DateTime, Task> callback);
        Task GetBatchesForEndpoint(DateTime cutoff, string endpoint, Func<string, DateTime, Task> callback);
        Task GetBatchesForFailedQueueAddress(DateTime cutoff, string failedQueueAddresspoint, FailedMessageStatus status, Func<string, DateTime, Task> callback);
        Task GetBatchesForFailureGroup(string groupId, string groupTitle, string groupType, DateTime cutoff, Func<string, DateTime, Task> callback);

        // RetryAllInGroupHandler
        Task<FailureGroupView> QueryFailureGroupViewOnGroupId(string groupId);
    }
}