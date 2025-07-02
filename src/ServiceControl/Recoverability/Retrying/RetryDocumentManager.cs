namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Persistence;

    class RetryDocumentManager
    {
        public RetryDocumentManager(IHostApplicationLifetime applicationLifetime, IRetryDocumentDataStore store, RetryingManager operationManager, ILogger<RetryDocumentManager> logger)
        {
            this.store = store;
            applicationLifetime?.ApplicationStopping.Register(() => { abort = true; });
            this.operationManager = operationManager;
            this.logger = logger;
        }

        public async Task<bool> AdoptOrphanedBatches()
        {
            var orphanedBatches = await store.QueryOrphanedBatches(RetrySessionId);

            logger.LogInformation("Found {OrphanedBatchCount} orphaned retry batches from previous sessions", orphanedBatches.Results.Count);

            // let's leave Task.Run for now due to sync sends
            await Task.WhenAll(orphanedBatches.Results.Select(b => Task.Run(async () =>
            {
                logger.LogInformation("Adopting retry batch {BatchId} with {BatchMessageCount} messages", b.Id, b.FailureRetries.Count);
                await MoveBatchToStaging(b.Id);
            })));

            foreach (var batch in orphanedBatches.Results)
            {
                if (batch.RetryType != RetryType.MultipleMessages)
                {
                    operationManager.Fail(batch.RetryType, batch.RequestId);
                }
            }

            if (abort)
            {
                return false;
            }

            return orphanedBatches.QueryStats.IsStale || orphanedBatches.Results.Any();
        }

        public virtual Task MoveBatchToStaging(string batchDocumentId) => store.MoveBatchToStaging(batchDocumentId);

        public async Task RebuildRetryOperationState()
        {
            var stagingBatchGroups = await store.QueryAvailableBatches();

            foreach (var group in stagingBatchGroups)
            {
                if (!string.IsNullOrWhiteSpace(group.RequestId))
                {
                    logger.LogDebug("Rebuilt retry operation status for {RetryType}/{RetryRequestId}. Aggregated batchsize: {RetryBatchSize}", group.RetryType, group.RequestId, group.InitialBatchSize);

                    await operationManager.PreparedAdoptedBatch(group.RequestId, group.RetryType, group.InitialBatchSize, group.InitialBatchSize, group.Originator, group.Classifier, group.StartTime, group.Last);
                }
            }
        }

        readonly RetryingManager operationManager;
        readonly IRetryDocumentDataStore store;
        bool abort;
        public static string RetrySessionId = Guid.NewGuid().ToString();

        readonly ILogger<RetryDocumentManager> logger;
    }
}