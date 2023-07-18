namespace ServiceControl.Persistence
{
    using System.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using Infrastructure;
    using ServiceControl.MessageFailures;

    public interface IRetryDocumentDataStore
    {
        Task StageRetryByUniqueMessageIds(string batchDocumentId, string requestId, RetryType retryType, string[] messageIds,
            DateTime startTime, DateTime? last = null, string originator = null, string batchName = null,
            string classifier = null);

        Task MoveBatchToStaging(string batchDocumentId);

        Task<string> CreateBatchDocument(string retrySessionId, string requestId, RetryType retryType,
            string[] failedMessageRetryIds, string originator, DateTime startTime, DateTime? last = null,
            string batchName = null, string classifier = null);

        Task<QueryResult<IList<RetryBatch>>> QueryOrphanedBatches(string retrySessionId, DateTime cutoff);
        Task<IList<RetryBatchGroup>> QueryAvailableBatches();

        // RetriesGateway
        Task GetBatchesForAll(DateTime cutoff, Func<string, DateTime, Task> callback);
        Task GetBatchesForEndpoint(DateTime cutoff, string endpoint, Func<string, DateTime, Task> callback);
        Task GetBatchesForFailedQueueAddress(DateTime cutoff, string failedQueueAddresspoint, FailedMessageStatus status, Func<string, DateTime, Task> callback);
    }
}