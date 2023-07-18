namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using Persistence.Infrastructure;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Client.Linq;
    using Raven.Json.Linq;
    using ServiceControl.Persistence;

    class RetryDocumentManager
    {
        public RetryDocumentManager(IHostApplicationLifetime applicationLifetime, IRetryDocumentDataStore store, RetryingManager operationManager)
        {
            this.store = store;
            applicationLifetime?.ApplicationStopping.Register(() => { abort = true; });
            this.operationManager = operationManager;
        }

        internal async Task<bool> AdoptOrphanedBatches(DateTime cutoff)
        {
            var orphanedBatches = await store.QueryOrphanedBatches(RetrySessionId, cutoff).ConfigureAwait(false);

            log.Info($"Found {orphanedBatches.Results.Count} orphaned retry batches from previous sessions.");

            // let's leave Task.Run for now due to sync sends
            await Task.WhenAll(orphanedBatches.Results.Select(b => Task.Run(async () =>
            {
                log.Info($"Adopting retry batch {b.Id} with {b.FailureRetries.Count} messages.");
                await store.MoveBatchToStaging(b.Id).ConfigureAwait(false);
            }))).ConfigureAwait(false);

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


        internal async Task RebuildRetryOperationState(IAsyncDocumentSession session)
        {
            var stagingBatchGroups = await session.Query<RetryBatchGroup, RetryBatches_ByStatus_ReduceInitialBatchSize>()
                .Where(b => b.HasStagingBatches || b.HasForwardingBatches)
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var group in stagingBatchGroups)
            {
                if (!string.IsNullOrWhiteSpace(group.RequestId))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Rebuilt retry operation status for {0}/{1}. Aggregated batchsize: {2}", group.RetryType, group.RequestId, group.InitialBatchSize);
                    }

                    await operationManager.PreparedAdoptedBatch(group.RequestId, group.RetryType, group.InitialBatchSize, group.InitialBatchSize, group.Originator, group.Classifier, group.StartTime, group.Last)
                        .ConfigureAwait(false);
                }
            }
        }

        RetryingManager operationManager;
        IRetryDocumentDataStore store;
        bool abort;
        public static string RetrySessionId = Guid.NewGuid().ToString();

        

        static ILog log = LogManager.GetLogger(typeof(RetryDocumentManager));
    }
}