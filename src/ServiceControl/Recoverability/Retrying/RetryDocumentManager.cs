namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure;
    using MessageFailures;
    using NServiceBus.Logging;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Client.Linq;
    using Raven.Json.Linq;

    public class RetryDocumentManager
    {
        public RetryDocumentManager(ShutdownNotifier notifier, IDocumentStore store)
        {
            this.store = store;
            notifier.Register(() => { abort = true; });
        }

        public RetryingManager OperationManager { get; set; }

        public async Task<string> CreateBatchDocument(string requestId, RetryType retryType, string[] failedMessageRetryIds, string originator, DateTime startTime, DateTime? last = null, string batchName = null, string classifier = null)
        {
            var batchDocumentId = RetryBatch.MakeDocumentId(Guid.NewGuid().ToString());
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(new RetryBatch
                {
                    Id = batchDocumentId,
                    Context = batchName,
                    RequestId = requestId,
                    RetryType = retryType,
                    Originator = originator,
                    Classifier = classifier,
                    StartTime = startTime,
                    Last = last,
                    InitialBatchSize = failedMessageRetryIds.Length,
                    RetrySessionId = RetrySessionId,
                    FailureRetries = failedMessageRetryIds,
                    Status = RetryBatchStatus.MarkingDocuments
                }).ConfigureAwait(false);
                await session.SaveChangesAsync().ConfigureAwait(false);
            }

            return batchDocumentId;
        }

        public ICommandData CreateFailedMessageRetryDocument(string batchDocumentId, string messageId)
        {
            return new PatchCommandData
            {
                Patches = patchRequestsEmpty,
                PatchesIfMissing = new[]
                {
                    new PatchRequest
                    {
                        Name = "FailedMessageId",
                        Type = PatchCommandType.Set,
                        Value = FailedMessage.MakeDocumentId(messageId)
                    },
                    new PatchRequest
                    {
                        Name = "RetryBatchId",
                        Type = PatchCommandType.Set,
                        Value = batchDocumentId
                    }
                },
                Key = FailedMessageRetry.MakeDocumentId(messageId),
                Metadata = defaultMetadata
            };
        }

        public virtual async Task MoveBatchToStaging(string batchDocumentId)
        {
            try
            {
                await store.AsyncDatabaseCommands.PatchAsync(batchDocumentId,
                    new[]
                    {
                        new PatchRequest
                        {
                            Type = PatchCommandType.Set,
                            Name = "Status",
                            Value = (int)RetryBatchStatus.Staging,
                            PrevVal = (int)RetryBatchStatus.MarkingDocuments
                        }
                    }).ConfigureAwait(false);
            }
            catch (ConcurrencyException)
            {
                log.DebugFormat("Ignoring concurrency exception while moving batch to staging {0}", batchDocumentId);
            }
        }

        public Task RemoveFailedMessageRetryDocument(string uniqueMessageId)
        {
            return store.AsyncDatabaseCommands.DeleteAsync(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null);
        }

        internal async Task<bool> AdoptOrphanedBatches(IAsyncDocumentSession session, DateTime cutoff)
        {
            var orphanedBatches = await session.Query<RetryBatch, RetryBatches_ByStatusAndSession>()
                .Customize(c => c.BeforeQueryExecution(index => index.Cutoff = cutoff))
                .Where(b => b.Status == RetryBatchStatus.MarkingDocuments && b.RetrySessionId != RetrySessionId)
                .Statistics(out var stats)
                .ToListAsync()
                .ConfigureAwait(false);

            log.InfoFormat("Found {0} orphaned retry batches from previous sessions", orphanedBatches.Count);

            // let's leave Task.Run for now due to sync sends
            await Task.WhenAll(orphanedBatches.Select(b => Task.Run(async () =>
            {
                log.InfoFormat("Adopting retry batch {0} from previous session with {1} messages", b.Id, b.FailureRetries.Count);
                await MoveBatchToStaging(b.Id).ConfigureAwait(false);
            }))).ConfigureAwait(false);

            foreach (var batch in orphanedBatches)
            {
                if (batch.RetryType != RetryType.MultipleMessages)
                {
                    OperationManager.Fail(batch.RetryType, batch.RequestId);
                }
            }

            if (abort)
            {
                return false;
            }

            return stats.IsStale || orphanedBatches.Any();
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
                    log.DebugFormat("Rebuilt retry operation status for {0}/{1}. Aggregated batchsize: {2}", group.RetryType, group.RequestId, group.InitialBatchSize);
                    await OperationManager.PreparedAdoptedBatch(group.RequestId, group.RetryType, group.InitialBatchSize, group.InitialBatchSize, group.Originator, group.Classifier, group.StartTime, group.Last)
                        .ConfigureAwait(false);
                }
            }
        }

        private IDocumentStore store;
        private bool abort;
        protected static string RetrySessionId = Guid.NewGuid().ToString();

        private static RavenJObject defaultMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessageRetry.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessageRetry).AssemblyQualifiedName}""
                                    }}");

        private static PatchRequest[] patchRequestsEmpty = new PatchRequest[0];

        static ILog log = LogManager.GetLogger(typeof(RetryDocumentManager));
    }
}