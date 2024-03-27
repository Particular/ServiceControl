namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using ServiceControl.Persistence;

    class RetryDocumentManager
    {
        public RetryDocumentManager(IHostApplicationLifetime applicationLifetime, IRetryDocumentDataStore store, RetryingManager operationManager)
        {
            this.store = store;
            applicationLifetime?.ApplicationStopping.Register(() => { abort = true; });
            this.operationManager = operationManager;
        }

        public async Task<bool> AdoptOrphanedBatches()
        {
            var orphanedBatches = await store.QueryOrphanedBatches(RetrySessionId);

            log.Info($"Found {orphanedBatches.Results.Count} orphaned retry batches from previous sessions.");

            // let's leave Task.Run for now due to sync sends
            await Task.WhenAll(orphanedBatches.Results.Select(b => Task.Run(async () =>
            {
                log.Info($"Adopting retry batch {b.Id} with {b.FailureRetries.Count} messages.");
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
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Rebuilt retry operation status for {0}/{1}. Aggregated batchsize: {2}", group.RetryType, group.RequestId, group.InitialBatchSize);
                    }

                    await operationManager.PreparedAdoptedBatch(group.RequestId, group.RetryType, group.InitialBatchSize, group.InitialBatchSize, group.Originator, group.Classifier, group.StartTime, group.Last);
                }
            }
        }

        readonly RetryingManager operationManager;
        readonly IRetryDocumentDataStore store;
        bool abort;
        public static string RetrySessionId = Guid.NewGuid().ToString();

        static readonly ILog log = LogManager.GetLogger(typeof(RetryDocumentManager));
    }
}