namespace ServiceControl.Persistence.Sql;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

class NoOpRetryDocumentDataStore : IRetryDocumentDataStore
{
    public Task StageRetryByUniqueMessageIds(string batchDocumentId, string[] messageIds) => Task.CompletedTask;

    public Task MoveBatchToStaging(string batchDocumentId) => Task.CompletedTask;

    public Task<string> CreateBatchDocument(string retrySessionId, string requestId, RetryType retryType,
        string[] failedMessageRetryIds, string originator, DateTime startTime, DateTime? last = null,
        string batchName = null, string classifier = null) =>
        Task.FromResult(string.Empty);

    public Task<QueryResult<IList<RetryBatch>>> QueryOrphanedBatches(string retrySessionId) =>
        Task.FromResult(new QueryResult<IList<RetryBatch>>([], QueryStatsInfo.Zero));

    public Task<IList<RetryBatchGroup>> QueryAvailableBatches() =>
        Task.FromResult<IList<RetryBatchGroup>>([]);

    public Task GetBatchesForAll(DateTime cutoff, Func<string, DateTime, Task> callback) => Task.CompletedTask;

    public Task GetBatchesForEndpoint(DateTime cutoff, string endpoint, Func<string, DateTime, Task> callback) =>
        Task.CompletedTask;

    public Task GetBatchesForFailedQueueAddress(DateTime cutoff, string failedQueueAddresspoint,
        FailedMessageStatus status, Func<string, DateTime, Task> callback) => Task.CompletedTask;

    public Task GetBatchesForFailureGroup(string groupId, string groupTitle, string groupType, DateTime cutoff,
        Func<string, DateTime, Task> callback) => Task.CompletedTask;

    public Task<FailureGroupView> QueryFailureGroupViewOnGroupId(string groupId) =>
        Task.FromResult<FailureGroupView>(null);
}
