namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MessageFailures;
    using NServiceBus.Logging;
    using Persistence.Infrastructure;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Exceptions;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Recoverability;

    class RetryDocumentDataStore(IRavenSessionProvider sessionProvider, IRavenDocumentStoreProvider documentStoreProvider) : IRetryDocumentDataStore
    {
        public async Task StageRetryByUniqueMessageIds(string batchDocumentId, string[] messageIds)
        {
            var commands = new ICommandData[messageIds.Length];

            for (var i = 0; i < messageIds.Length; i++)
            {
                commands[i] = CreateFailedMessageRetryDocument(batchDocumentId, messageIds[i]);
            }

            using var session = await sessionProvider.OpenSession();
            var documentStore = await documentStoreProvider.GetDocumentStore();
            var batch = new SingleNodeBatchCommand(documentStore.Conventions, session.Advanced.Context, commands);
            await session.Advanced.RequestExecutor.ExecuteAsync(batch, session.Advanced.Context);
        }

        public async Task MoveBatchToStaging(string batchDocumentId)
        {
            try
            {
                var documentStore = await documentStoreProvider.GetDocumentStore();
                await documentStore.Operations.SendAsync(new PatchOperation(batchDocumentId, null, new PatchRequest
                {
                    Script = @"this.Status = args.Status",
                    Values =
                    {
                        {"Status", (int)RetryBatchStatus.Staging }
                    }
                }));
            }
            catch (ConcurrencyException)
            {
                Logger.DebugFormat("Ignoring concurrency exception while moving batch to staging {0}", batchDocumentId);
            }
        }

        public async Task<string> CreateBatchDocument(string retrySessionId, string requestId, RetryType retryType, string[] failedMessageRetryIds,
            string originator,
            DateTime startTime, DateTime? last = null, string batchName = null, string classifier = null)
        {
            var batchDocumentId = RetryBatch.MakeDocumentId(Guid.NewGuid().ToString());
            using var session = await sessionProvider.OpenSession();
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
                RetrySessionId = retrySessionId,
                FailureRetries = failedMessageRetryIds,
                Status = RetryBatchStatus.MarkingDocuments
            });
            await session.SaveChangesAsync();

            return batchDocumentId;
        }

        public async Task<QueryResult<IList<RetryBatch>>> QueryOrphanedBatches(string retrySessionId)
        {
            using var session = await sessionProvider.OpenSession();
            var orphanedBatches = await session
                .Query<RetryBatch, RetryBatches_ByStatusAndSession>()

                .Where(b => b.Status == RetryBatchStatus.MarkingDocuments && b.RetrySessionId != retrySessionId)
                .Statistics(out var stats)
                .ToListAsync();

            return orphanedBatches.ToQueryResult(stats);
        }

        public async Task<IList<RetryBatchGroup>> QueryAvailableBatches()
        {
            using var session = await sessionProvider.OpenSession();
            var results = await session.Query<RetryBatchGroup, RetryBatches_ByStatus_ReduceInitialBatchSize>()
                .Where(b => b.HasStagingBatches || b.HasForwardingBatches)
                .ToListAsync();
            return results;
        }

        static ICommandData CreateFailedMessageRetryDocument(string batchDocumentId, string messageId)
        {
            var patchRequest = new PatchRequest
            {
                Script = @"this.FailedMessageId = args.MessageId
                           this.RetryBatchId = args.BatchDocumentId",
                Values =
                {
                    { "MessageId", FailedMessageIdGenerator.MakeDocumentId(messageId) },
                    { "BatchDocumentId", batchDocumentId }
                }
            };

            return new PatchCommandData(FailedMessageRetry.MakeDocumentId(messageId), null, patch: new PatchRequest { Script = "" }, patchIfMissing: patchRequest);
        }

        public async Task GetBatchesForAll(DateTime cutoff, Func<string, DateTime, Task> callback)
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Where(d => d.Status == FailedMessageStatus.Unresolved)
                .Select(m => new
                {
                    UniqueMessageId = m.MessageId,
                    LatestTimeOfFailure = m.TimeOfFailure
                });

            await using var stream = await session.Advanced.StreamAsync(query);
            while (await stream.MoveNextAsync())
            {
                var current = stream.Current.Document;
                await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
            }
        }

        public async Task GetBatchesForEndpoint(DateTime cutoff, string endpoint, Func<string, DateTime, Task> callback)
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Where(d => d.Status == FailedMessageStatus.Unresolved)
                .Where(m => m.ReceivingEndpointName == endpoint)
                .Select(m => new
                {
                    UniqueMessageId = m.MessageId,
                    LatestTimeOfFailure = m.TimeOfFailure
                });

            await using var stream = await session.Advanced.StreamAsync(query);
            while (await stream.MoveNextAsync())
            {
                var current = stream.Current.Document;
                await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
            }
        }

        public async Task GetBatchesForFailedQueueAddress(DateTime cutoff, string failedQueueAddress, FailedMessageStatus status, Func<string, DateTime, Task> callback)
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Where(d => d.Status == FailedMessageStatus.Unresolved)
                .Where(m => m.QueueAddress == failedQueueAddress && m.Status == status)
                .Select(m => new
                {
                    UniqueMessageId = m.MessageId,
                    LatestTimeOfFailure = m.TimeOfFailure
                });

            await using var stream = await session.Advanced.StreamAsync(query);
            while (await stream.MoveNextAsync())
            {
                var current = stream.Current.Document;
                await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
            }
        }

        public async Task GetBatchesForFailureGroup(string groupId, string groupTitle, string groupType, DateTime cutoff, Func<string, DateTime, Task> callback)
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Query<FailureGroupMessageView, FailedMessages_ByGroup>()
                .Where(d => d.Status == FailedMessageStatus.Unresolved)
                .Where(m => m.FailureGroupId == groupId)
                .Select(m => new
                {
                    UniqueMessageId = m.MessageId,
                    LatestTimeOfFailure = m.TimeOfFailure
                });

            await using var stream = await session.Advanced.StreamAsync(query);
            while (await stream.MoveNextAsync())
            {
                var current = stream.Current.Document;
                await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
            }
        }

        public async Task<FailureGroupView> QueryFailureGroupViewOnGroupId(string groupId)
        {
            using var session = await sessionProvider.OpenSession();
            var group = await session.Query<FailureGroupView, FailureGroupsViewIndex>()
                .FirstOrDefaultAsync(x => x.Id == groupId);
            return group;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RetryDocumentDataStore));
    }
}