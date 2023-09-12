namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MessageFailures;
    using Newtonsoft.Json.Linq;
    using NServiceBus.Logging;
    using Persistence.Infrastructure;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Exceptions;
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
                var orphanedBatches = await session
                    .Query<RetryBatch, RetryBatches_ByStatusAndSession>()

                    // TODO: Cutoff no longer exists but guidance isn't clear how to handle this:
                    // https://ravendb.net/docs/article-page/5.4/Csharp/indexes/stale-indexes
                    // https://ravendb.net/docs/article-page/5.4/csharp/client-api/session/querying/how-to-customize-query#waitfornonstaleresults

                    //.Customize(c => c.BeforeQueryExecuted(index => index.Cutoff = cutoff))
                    .Customize(c => c.WaitForNonStaleResults()) // (ramon) I think this is valid as at start orphaned batches should be retrieved based on non-stale results I would assume?

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
                        Value = FailedMessageIdGenerator.MakeDocumentId(messageId)
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

        static JObject DefaultMetadata = JObject.Parse($@"
                                    {{
                                        ""@collection"": ""{FailedMessageRetry.CollectionName}"",
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessageRetry).AssemblyQualifiedName}""
                                    }}");

        static PatchRequest[] PatchRequestsEmpty = Array.Empty<PatchRequest>();

        static ILog log = LogManager.GetLogger(typeof(RetryDocumentDataStore));

        // TODO: Verify Stream queries in this file, which were the result of joining overly-complex IndexBasedBulkRetryRequest
        // which was in this file, as well as the FailedMessages_UniqueMessageIdAndTimeOfFailures transformer, since transformers
        // are not supported in RavenDB 5. I don't know what all the other properties of IndexBasedBulkRetryRequest were ever for,
        // since they weren't used in this class. I also don't know what the other comments that were in each streaming query method
        // were for either.

        public async Task GetBatchesForAll(DateTime cutoff, Func<string, DateTime, Task> callback)
        {
            // StartRetryForIndex<FailedMessage, FailedMessageViewIndex>("All", RetryType.All, DateTime.UtcNow, originator: "all messages");
            //public void StartRetryForIndex<TType, TIndex>(string requestId, RetryType retryType, DateTime startTime, Expression<Func<TType, bool>> filter = null, string originator = null, string classifier = null)
            //StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(endpoint, RetryType.AllForEndpoint, DateTime.UtcNow, m => m.ReceivingEndpointName == endpoint, $"all messages for endpoint {endpoint}");

            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Where(d => d.Status == FailedMessageStatus.Unresolved)
                    .Select(m => new
                    {
                        UniqueMessageId = m.MessageId,
                        LatestTimeOfFailure = m.TimeOfFailure
                    });

                await using (var stream = await session.Advanced.StreamAsync(query))
                {
                    while (await stream.MoveNextAsync())
                    {
                        var current = stream.Current.Document;
                        await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
                    }
                }
            }
        }

        public async Task GetBatchesForEndpoint(DateTime cutoff, string endpoint, Func<string, DateTime, Task> callback)
        {
            //ForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>
            //StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(endpoint, RetryType.AllForEndpoint, DateTime.UtcNow, m => m.ReceivingEndpointName == endpoint, $"all messages for endpoint {endpoint}");

            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Where(d => d.Status == FailedMessageStatus.Unresolved)
                    .Where(m => m.ReceivingEndpointName == endpoint)
                    .Select(m => new
                    {
                        UniqueMessageId = m.MessageId,
                        LatestTimeOfFailure = m.TimeOfFailure
                    });

                await using (var stream = await session.Advanced.StreamAsync(query))
                {
                    while (await stream.MoveNextAsync())
                    {
                        var current = stream.Current.Document;
                        await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
                    }
                }
            }
        }

        public async Task GetBatchesForFailedQueueAddress(DateTime cutoff, string failedQueueAddress, FailedMessageStatus status, Func<string, DateTime, Task> callback)
        {
            //ForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>
            //StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(failedQueueAddress, RetryType.ByQueueAddress, DateTime.UtcNow, m => m.QueueAddress == failedQueueAddress && m.Status == status, );

            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Where(d => d.Status == FailedMessageStatus.Unresolved)
                    .Where(m => m.QueueAddress == failedQueueAddress && m.Status == status)
                    .Select(m => new
                    {
                        UniqueMessageId = m.MessageId,
                        LatestTimeOfFailure = m.TimeOfFailure
                    });

                await using (var stream = await session.Advanced.StreamAsync(query))
                {
                    while (await stream.MoveNextAsync())
                    {
                        var current = stream.Current.Document;
                        await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
                    }
                }
            }
        }

        public async Task GetBatchesForFailureGroup(string groupId, string groupTitle, string groupType, DateTime cutoff, Func<string, DateTime, Task> callback)
        {
            //retries.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>(message.GroupId, RetryType.FailureGroup, started, x => x.FailureGroupId == message.GroupId, originator, group?.Type);

            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .Where(d => d.Status == FailedMessageStatus.Unresolved)
                    .Where(m => m.FailureGroupId == groupId)
                    .Select(m => new
                    {
                        UniqueMessageId = m.MessageId,
                        LatestTimeOfFailure = m.TimeOfFailure
                    });

                await using (var stream = await session.Advanced.StreamAsync(query))
                {
                    while (await stream.MoveNextAsync())
                    {
                        var current = stream.Current.Document;
                        await callback(current.UniqueMessageId, current.LatestTimeOfFailure);
                    }
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
    }
}