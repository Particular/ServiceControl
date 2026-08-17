namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageFailures;
    using Microsoft.Extensions.Logging;
    using Persistence.Infrastructure;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Exceptions;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Recoverability;

    class RetryDocumentDataStore(IRavenSessionProvider sessionProvider, IRavenDocumentStoreProvider documentStoreProvider, ILogger<RetryDocumentDataStore> logger) : IRetryBatchStore
    {
        public async Task AssignMessagesToBatch(string batchId, string[] messageIds, CancellationToken cancellationToken = default)
        {
            var commands = new ICommandData[messageIds.Length];

            for (var i = 0; i < messageIds.Length; i++)
            {
                commands[i] = CreateFailedMessageRetryDocument(batchId, messageIds[i]);
            }

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var batch = new SingleNodeBatchCommand(documentStore.Conventions, session.Advanced.Context, commands);
            await session.Advanced.RequestExecutor.ExecuteAsync(batch, session.Advanced.Context, token: cancellationToken);
        }

        public async Task MoveBatchToStaging(string batchId, CancellationToken cancellationToken = default)
        {
            try
            {
                var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
                await documentStore.Operations.SendAsync(new PatchOperation(batchId, null, new PatchRequest
                {
                    Script = @"this.Status = args.Status",
                    Values =
                    {
                        {"Status", (int)RetryBatchStatus.Staging }
                    }
                }), token: cancellationToken);
            }
            catch (ConcurrencyException)
            {
                logger.LogDebug("Ignoring concurrency exception while moving batch to staging {BatchDocumentId}", batchId);
            }
        }

        public async Task<string> CreateBatch(string retrySessionId, string requestId, RetryType retryType, string[] failedMessageRetryIds,
            string originator,
            DateTime startTime, DateTime? last = null, string batchName = null, string classifier = null,
            string initiatedById = null, string initiatedByName = null, string operationId = null,
            CancellationToken cancellationToken = default)
        {
            var batchId = MakeDocumentId(Guid.NewGuid().ToString());
            failedMessageRetryIds = failedMessageRetryIds.Select(MakeFailedMessageRetriesDocumentId).ToArray();
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            await session.StoreAsync(new RetryBatch
            {
                Id = batchId,
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
                Status = RetryBatchStatus.MarkingDocuments,
                InitiatedById = initiatedById,
                InitiatedByName = initiatedByName,
                OperationId = operationId
            }, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            return batchId;
        }

        public async Task<QueryResult<IList<Persistence.RetryBatch>>> GetOrphanedBatches(string retrySessionId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var orphanedBatches = await session
                .Query<RetryBatch, RetryBatches_ByStatusAndSession>()

                .Where(b => b.Status == RetryBatchStatus.MarkingDocuments && b.RetrySessionId != retrySessionId)
                .Statistics(out var stats)
                .ToListAsync(cancellationToken);

            return orphanedBatches.Select(batch => batch.ToContract()).ToList().ToQueryResult(stats);
        }

        public async Task<IList<RetryBatchGroup>> GetAvailableBatchGroups(CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var results = await session.Query<RetryBatchGroup, RetryBatches_ByStatus_ReduceInitialBatchSize>()
                .Where(b => b.HasStagingBatches || b.HasForwardingBatches)
                .ToListAsync(cancellationToken);
            return results;
        }

        static ICommandData CreateFailedMessageRetryDocument(string batchId, string messageId)
        {
            var patchRequest = new PatchRequest
            {
                Script = @"this.FailedMessageId = args.MessageId
                           this.RetryBatchId = args.BatchDocumentId",
                Values =
                {
                    { "MessageId", FailedMessageIdGenerator.MakeDocumentId(messageId) },
                    { "BatchDocumentId", batchId }
                }
            };

            return new PatchCommandData(MakeFailedMessageRetriesDocumentId(messageId), null, patch: new PatchRequest { Script = "" }, patchIfMissing: patchRequest);
        }

        public async Task ForEachUnresolvedMessage(Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Where(d => d.Status == FailedMessageStatus.Unresolved)
                .Select(m => new
                {
                    UniqueMessageId = m.MessageId,
                    LatestTimeOfFailure = m.TimeOfFailure
                });

            await using var stream = await session.Advanced.StreamAsync(query, cancellationToken);
            while (await stream.MoveNextAsync())
            {
                var current = stream.Current.Document;
                await callback(current.UniqueMessageId, current.LatestTimeOfFailure, cancellationToken);
            }
        }

        public async Task ForEachUnresolvedMessageForEndpoint(string endpoint, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Where(d => d.Status == FailedMessageStatus.Unresolved)
                .Where(m => m.ReceivingEndpointName == endpoint)
                .Select(m => new
                {
                    UniqueMessageId = m.MessageId,
                    LatestTimeOfFailure = m.TimeOfFailure
                });

            await using var stream = await session.Advanced.StreamAsync(query, cancellationToken);
            while (await stream.MoveNextAsync())
            {
                var current = stream.Current.Document;
                await callback(current.UniqueMessageId, current.LatestTimeOfFailure, cancellationToken);
            }
        }

        public async Task ForEachMessageForQueueAddress(string failedQueueAddress, FailedMessageStatus status, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Where(d => d.Status == FailedMessageStatus.Unresolved)
                .Where(m => m.QueueAddress == failedQueueAddress && m.Status == status)
                .Select(m => new
                {
                    UniqueMessageId = m.MessageId,
                    LatestTimeOfFailure = m.TimeOfFailure
                });

            await using var stream = await session.Advanced.StreamAsync(query, cancellationToken);
            while (await stream.MoveNextAsync())
            {
                var current = stream.Current.Document;
                await callback(current.UniqueMessageId, current.LatestTimeOfFailure, cancellationToken);
            }
        }

        public async Task ForEachUnresolvedMessageInGroup(string groupId, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = session.Query<FailureGroupMessageView, FailedMessages_ByGroup>()
                .Where(d => d.Status == FailedMessageStatus.Unresolved)
                .Where(m => m.FailureGroupId == groupId)
                .Select(m => new
                {
                    UniqueMessageId = m.MessageId,
                    LatestTimeOfFailure = m.TimeOfFailure
                });

            await using var stream = await session.Advanced.StreamAsync(query, cancellationToken);
            while (await stream.MoveNextAsync())
            {
                var current = stream.Current.Document;
                await callback(current.UniqueMessageId, current.LatestTimeOfFailure, cancellationToken);
            }
        }

        public async Task<ForwardingRetryBatch> GetCurrentForwardingBatch(CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var nowForwarding = await session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .LoadAsync<RetryBatchNowForwarding>(NowForwardingDocumentId, cancellationToken);

            if (nowForwarding == null)
            {
                return null;
            }

            var batch = await session.LoadAsync<RetryBatch>(nowForwarding.RetryBatchId, cancellationToken);

            return batch == null
                ? null
                : new ForwardingRetryBatch(batch.RequestId, batch.RetryType, batch.Originator, batch.Classifier);
        }

        public static string MakeDocumentId(string messageUniqueId) => "RetryBatches/" + messageUniqueId;

        public static string MakeFailedMessageRetriesDocumentId(string messageUniqueId) => "FailedMessageRetries/" + messageUniqueId;

        public static readonly string NowForwardingDocumentId = MakeDocumentId("NowForwarding");
    }
}