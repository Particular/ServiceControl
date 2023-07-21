namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using MessageFailures;
    using NServiceBus.Logging;
    using Persistence.Infrastructure;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;
    using Raven.Json.Linq;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Recoverability;

    class RetryDocumentDataStore : IRetryDocumentDataStore
    {
        readonly IDocumentStore store;

        public RetryDocumentDataStore(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task StageRetryByUniqueMessageIds(string batchDocumentId, string requestId, RetryType retryType, string[] messageIds,
            DateTime startTime,
            DateTime? last = null, string originator = null, string batchName = null, string classifier = null)
        {
            var commands = new ICommandData[messageIds.Length];

            for (var i = 0; i < messageIds.Length; i++)
            {
                commands[i] = CreateFailedMessageRetryDocument(batchDocumentId, messageIds[i]);
            }

            await store.AsyncDatabaseCommands.BatchAsync(commands);
        }

        public async Task MoveBatchToStaging(string batchDocumentId)
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
                    });
            }
            catch (ConcurrencyException)
            {
                log.DebugFormat("Ignoring concurrency exception while moving batch to staging {0}", batchDocumentId);
            }
        }

        public async Task<string> CreateBatchDocument(string retrySessionId, string requestId, RetryType retryType, string[] failedMessageRetryIds,
            string originator,
            DateTime startTime, DateTime? last = null, string batchName = null, string classifier = null)
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
                    RetrySessionId = retrySessionId,
                    FailureRetries = failedMessageRetryIds,
                    Status = RetryBatchStatus.MarkingDocuments
                });
                await session.SaveChangesAsync();
            }

            return batchDocumentId;
        }

        public async Task<QueryResult<IList<RetryBatch>>> QueryOrphanedBatches(string retrySessionId, DateTime cutoff)
        {
            using (var session = store.OpenAsyncSession())
            {
                var orphanedBatches = await session.Query<RetryBatch, RetryBatches_ByStatusAndSession>()
                    .Customize(c => c.BeforeQueryExecution(index => index.Cutoff = cutoff))
                    .Where(b => b.Status == RetryBatchStatus.MarkingDocuments && b.RetrySessionId != retrySessionId)
                    .Statistics(out var stats)
                    .ToListAsync();

                return orphanedBatches.ToQueryResult(stats);
            }
        }

        public async Task<IList<RetryBatchGroup>> QueryAvailableBatches()
        {
            using (var session = store.OpenAsyncSession())
            {
                var results = await session.Query<RetryBatchGroup, RetryBatches_ByStatus_ReduceInitialBatchSize>()
                    .Where(b => b.HasStagingBatches || b.HasForwardingBatches)
                    .ToListAsync();
                return results;
            }
        }

        static ICommandData CreateFailedMessageRetryDocument(string batchDocumentId, string messageId)
        {
            return new PatchCommandData
            {
                Patches = PatchRequestsEmpty,
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
                Metadata = DefaultMetadata
            };
        }

        static RavenJObject DefaultMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessageRetry.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessageRetry).AssemblyQualifiedName}""
                                    }}");

        static PatchRequest[] PatchRequestsEmpty = Array.Empty<PatchRequest>();

        static ILog log = LogManager.GetLogger(typeof(RetryDocumentDataStore));



        public async Task GetBatchesForAll(DateTime cutoff, Func<string, DateTime, Task> callback)
        {
            // StartRetryForIndex<FailedMessage, FailedMessageViewIndex>("All", RetryType.All, DateTime.UtcNow, originator: "all messages");
            //public void StartRetryForIndex<TType, TIndex>(string requestId, RetryType retryType, DateTime startTime, Expression<Func<TType, bool>> filter = null, string originator = null, string classifier = null)
            //StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(endpoint, RetryType.AllForEndpoint, DateTime.UtcNow, m => m.ReceivingEndpointName == endpoint, $"all messages for endpoint {endpoint}");

            var x = new IndexBasedBulkRetryRequest<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(
                "All",
                RetryType.All,
                originator: "all messages",
                classifier: null,
                cutoff,
                filter: null
                );

            using (var session = store.OpenAsyncSession())
            using (var stream = await x.GetDocuments(session))
            {
                while (await stream.MoveNextAsync())
                {
                    var current = stream.Current.Document;
                    await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
                }
            }
        }

        public async Task GetBatchesForEndpoint(DateTime cutoff, string endpoint, Func<string, DateTime, Task> callback)
        {
            //ForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>
            //StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(endpoint, RetryType.AllForEndpoint, DateTime.UtcNow, m => m.ReceivingEndpointName == endpoint, $"all messages for endpoint {endpoint}");

            var x = new IndexBasedBulkRetryRequest<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(
                endpoint,
                RetryType.AllForEndpoint,
                originator: $"all messages for endpoint {endpoint}",
                classifier: null,
                cutoff,
                filter: m => m.ReceivingEndpointName == endpoint
            );

            using (var session = store.OpenAsyncSession())
            using (var stream = await x.GetDocuments(session))
            {
                while (await stream.MoveNextAsync())
                {
                    var current = stream.Current.Document;
                    await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
                }
            }
        }

        public async Task GetBatchesForFailedQueueAddress(DateTime cutoff, string failedQueueAddress, FailedMessageStatus status, Func<string, DateTime, Task> callback)
        {
            //ForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>
            //StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(failedQueueAddress, RetryType.ByQueueAddress, DateTime.UtcNow, m => m.QueueAddress == failedQueueAddress && m.Status == status, );

            var x = new IndexBasedBulkRetryRequest<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(
                failedQueueAddress,
                RetryType.AllForEndpoint,
                originator: $"all messages for failed queue address '{failedQueueAddress}'",
                classifier: null,
                cutoff,
                filter: m => m.QueueAddress == failedQueueAddress && m.Status == status
            );

            using (var session = store.OpenAsyncSession())
            using (var stream = await x.GetDocuments(session))
            {
                while (await stream.MoveNextAsync())
                {
                    var current = stream.Current.Document;
                    await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
                }
            }
        }

        public async Task GetBatchesForFailureGroup(string groupId, string groupTitle, string groupType, DateTime cutoff, Func<string, DateTime, Task> callback)
        {
            //retries.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>(message.GroupId, RetryType.FailureGroup, started, x => x.FailureGroupId == message.GroupId, originator, group?.Type);

            var x = new IndexBasedBulkRetryRequest<FailureGroupMessageView, FailedMessages_ByGroup>(
                groupId,
                RetryType.AllForEndpoint,
                originator: groupTitle,
                classifier: groupType,
                cutoff,
                filter: m => m.FailureGroupId == groupId
            );

            using (var session = store.OpenAsyncSession())
            using (var stream = await x.GetDocuments(session))
            {
                while (await stream.MoveNextAsync())
                {
                    var current = stream.Current.Document;
                    await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
                }
            }
        }

        public async Task<FailureGroupView> QueryFailureGroupViewOnGroupId(string groupId)
        {
            using (var session = store.OpenAsyncSession())
            {
                var group = await session.Query<FailureGroupView, FailureGroupsViewIndex>()
                    .FirstOrDefaultAsync(x => x.Id == groupId);
                return group;
            }
        }

        class IndexBasedBulkRetryRequest<TType, TIndex>
            where TIndex : AbstractIndexCreationTask, new()
            where TType : IHaveStatus
        {
            public IndexBasedBulkRetryRequest(string requestId, RetryType retryType, string originator, string classifier, DateTime startTime, Expression<Func<TType, bool>> filter)
            {
                RequestId = requestId;
                RetryType = retryType;
                Originator = originator;
                this.filter = filter;
                StartTime = startTime;
                Classifier = classifier;
            }

            public string RequestId { get; set; }
            public RetryType RetryType { get; set; }
            public string Originator { get; set; }
            public string Classifier { get; set; }
            public DateTime StartTime { get; set; }

            public Task<Raven.Abstractions.Util.IAsyncEnumerator<StreamResult<FailedMessages_UniqueMessageIdAndTimeOfFailures.Result>>> GetDocuments(IAsyncDocumentSession session)
            {
                var query = session.Query<TType, TIndex>();

                query = query.Where(d => d.Status == FailedMessageStatus.Unresolved);

                if (filter != null)
                {
                    query = query.Where(filter);
                }

                return session.Advanced.StreamAsync(query.TransformWith<FailedMessages_UniqueMessageIdAndTimeOfFailures, FailedMessages_UniqueMessageIdAndTimeOfFailures.Result>());
            }

            readonly Expression<Func<TType, bool>> filter;
        }
    }
}