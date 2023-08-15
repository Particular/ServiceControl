namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure;
    using MessageFailures;
    using NServiceBus.Logging;
    using ServiceControl.Persistence;

    class RetriesGateway
    {
        public RetriesGateway(IRetryDocumentDataStore store, RetryingManager operationManager)
        {
            this.store = store;
            this.operationManager = operationManager;
        }

        public async Task StartRetryForSingleMessage(string uniqueMessageId)
        {
            Log.Info($"Retrying a single message {uniqueMessageId}");

            var requestId = uniqueMessageId;
            var retryType = RetryType.SingleMessage;
            var numberOfMessages = 1;

            await operationManager.Preparing(requestId, retryType, numberOfMessages);
            await StageRetryByUniqueMessageIds(requestId, retryType, new[] { uniqueMessageId }, DateTime.UtcNow);
            await operationManager.PreparedBatch(requestId, retryType, numberOfMessages);
        }

        public async Task StartRetryForMessageSelection(string[] uniqueMessageIds)
        {
            Log.Info($"Retrying a selection of {uniqueMessageIds.Length} messages");

            var requestId = DeterministicGuid.MakeId(string.Join(string.Empty, uniqueMessageIds)).ToString();
            var retryType = RetryType.MultipleMessages;
            var numberOfMessages = uniqueMessageIds.Length;

            await operationManager.Preparing(requestId, retryType, numberOfMessages);
            await StageRetryByUniqueMessageIds(requestId, retryType, uniqueMessageIds, DateTime.UtcNow);
            await operationManager.PreparedBatch(requestId, retryType, numberOfMessages);
        }

        async Task StageRetryByUniqueMessageIds(string requestId, RetryType retryType, string[] messageIds, DateTime startTime, DateTime? last = null, string originator = null, string batchName = null, string classifier = null)
        {
            if (messageIds == null || !messageIds.Any())
            {
                Log.Info($"Batch '{batchName}' contains no messages");
                return;
            }

            var failedMessageRetryIds = messageIds.Select(FailedMessageRetry.MakeDocumentId).ToArray();

            var batchDocumentId = await store.CreateBatchDocument(RetryDocumentManager.RetrySessionId, requestId, retryType, failedMessageRetryIds, originator, startTime, last, batchName, classifier);

            Log.Info($"Created Batch '{batchDocumentId}' with {messageIds.Length} messages for '{batchName}'.");

            await store.StageRetryByUniqueMessageIds(batchDocumentId, requestId, retryType, messageIds, startTime, last,
                originator, batchName, classifier);

            await store.MoveBatchToStaging(batchDocumentId);

            Log.Info($"Moved Batch '{batchDocumentId}' to Staging");
        }

        // Needs to be overridable by a test
        protected virtual Task MoveBatchToStaging(string batchDocumentId) => store.MoveBatchToStaging(batchDocumentId);


        public async Task<bool> ProcessNextBulkRetry()  // Invoked from BulkRetryBatchCreationHostedService in schedule
        {
            if (!bulkRequests.TryDequeue(out var request))
            {
                return false;
            }

            await ProcessRequest(request);
            return true;
        }

        async Task ProcessRequest(BulkRetryRequest request)
        {
            var batchesWithLastAttempt = await request.GetRequestedBatches(store);
            var batches = batchesWithLastAttempt.Item1;
            var latestAttempt = batchesWithLastAttempt.Item2;
            var totalMessages = batches.Sum(b => b.Length);

            if (!operationManager.IsOperationInProgressFor(request.RequestId, request.RetryType) && totalMessages > 0)
            {
                var numberOfMessagesAdded = 0;

                await operationManager.Preparing(request.RequestId, request.RetryType, totalMessages);

                for (var i = 0; i < batches.Count; i++)
                {
                    await StageRetryByUniqueMessageIds(request.RequestId, request.RetryType, batches[i], request.StartTime, latestAttempt, request.Originator, GetBatchName(i + 1, batches.Count, request.Originator), request.Classifier);
                    numberOfMessagesAdded += batches[i].Length;

                    await operationManager.PreparedBatch(request.RequestId, request.RetryType, numberOfMessagesAdded);
                }
            }
        }

        static string GetBatchName(int pageNum, int totalPages, string context)
        {
            if (context == null)
            {
                return null;
            }

            return $"'{context}' batch {pageNum} of {totalPages}";
        }

        public void StartRetryForAllMessages()
        {
            var item = new RetryForAllMessages();
            Log.Info($"Enqueuing index based bulk retry '{item}'");
            bulkRequests.Enqueue(item);
        }

        public void StartRetryForEndpoint(string endpoint)
        {
            var item = new RetryForEndpoint(endpoint);
            Log.Info($"Enqueuing index based bulk retry '{item}'");
            bulkRequests.Enqueue(item);
        }

        public void StartRetryForFailedQueueAddress(string failedQueueAddress, FailedMessageStatus status)
        {
            var item = new RetryForFailedQueueAddress(failedQueueAddress, status);
            Log.Info($"Enqueuing index based bulk retry '{item}'");
            bulkRequests.Enqueue(item);
        }

        public void EnqueueRetryForFailureGroup(RetryForFailureGroup item)
        {
            Log.Info($"Enqueuing index based bulk retry '{item}'");
            bulkRequests.Enqueue(item);
        }

        readonly IRetryDocumentDataStore store;
        readonly RetryingManager operationManager;
        readonly ConcurrentQueue<BulkRetryRequest> bulkRequests = new ConcurrentQueue<BulkRetryRequest>();
        const int BatchSize = 1000;

        static readonly ILog Log = LogManager.GetLogger(typeof(RetriesGateway));

        public abstract class BulkRetryRequest
        {
            public string RequestId { get; }
            public RetryType RetryType { get; }
            public string Originator { get; }
            public string Classifier { get; }
            public DateTime StartTime { get; }

            public BulkRetryRequest(
                string requestId,
                RetryType retryType,
                DateTime startTime,
                string originator
                )
            {
                RequestId = requestId;
                RetryType = retryType;
                Originator = originator;
                StartTime = startTime;
            }

            protected abstract Task Invoke(IRetryDocumentDataStore store, Func<string, DateTime, Task> callback);

            public async Task<Tuple<List<string[]>, DateTime>> GetRequestedBatches(IRetryDocumentDataStore store)
            {
                var response = new List<string[]>();
                var currentBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var latestAttempt = DateTime.MinValue;

                Task Process(string uniqueMessageId, DateTime latestTimeOfFailure)
                {
                    currentBatch.Add(uniqueMessageId);

                    if (currentBatch.Count == BatchSize)
                    {
                        response.Add(currentBatch.ToArray());

                        currentBatch.Clear();
                    }

                    var lastDocumentAttempt = latestTimeOfFailure;
                    if (lastDocumentAttempt > latestAttempt)
                    {
                        latestAttempt = lastDocumentAttempt;
                    }

                    return Task.FromResult(response);
                }

                await Invoke(store, Process);

                if (currentBatch.Count > 0)
                {
                    response.Add(currentBatch.ToArray());
                }


                return Tuple.Create(response, latestAttempt);
            }
        }

        class RetryForAllMessages : BulkRetryRequest
        {
            public RetryForAllMessages() : base(requestId: "All", RetryType.All, DateTime.UtcNow, "all messages")
            {
            }

            protected override Task Invoke(IRetryDocumentDataStore store, Func<string, DateTime, Task> callback)
            {
                return store.GetBatchesForAll(StartTime, callback);
            }
        }

        class RetryForEndpoint : BulkRetryRequest
        {
            public string Endpoint { get; }

            public RetryForEndpoint(string endpoint) : base(requestId: endpoint, RetryType.AllForEndpoint, DateTime.UtcNow, originator: $"all messages for endpoint {endpoint}")
            {
                Endpoint = endpoint;
            }

            protected override Task Invoke(IRetryDocumentDataStore store, Func<string, DateTime, Task> callback)
            {
                return store.GetBatchesForEndpoint(StartTime, Endpoint, callback);
            }
        }

        public sealed class RetryForFailureGroup : BulkRetryRequest
        {
            public string GroupId { get; }
            public string GroupTitle { get; }
            public string GroupType { get; }

            public RetryForFailureGroup(string groupId, string groupTitle, string groupType, DateTime started) : base(requestId: groupId, RetryType.FailureGroup, started, originator: groupTitle)
            {
                GroupId = groupId;
                GroupType = groupType;
                GroupTitle = groupTitle;
            }

            protected override Task Invoke(IRetryDocumentDataStore store, Func<string, DateTime, Task> callback)
            {
                return store.GetBatchesForFailureGroup(
                    groupId: GroupId,
                    groupTitle: GroupTitle,
                    groupType: GroupType,
                    cutoff: StartTime,
                    callback
                    );
            }
        }

        class RetryForFailedQueueAddress : BulkRetryRequest
        {
            public string FailedQueueAddress { get; }
            public FailedMessageStatus Status { get; }


            public RetryForFailedQueueAddress(
                string failedQueueAddress,
                FailedMessageStatus status
                ) : base(requestId: failedQueueAddress, RetryType.ByQueueAddress, DateTime.UtcNow, originator: $"all messages for failed queue address '{failedQueueAddress}'")
            {
                FailedQueueAddress = failedQueueAddress;
                Status = status;
            }

            protected override Task Invoke(IRetryDocumentDataStore store, Func<string, DateTime, Task> callback)
            {
                return store.GetBatchesForFailedQueueAddress(StartTime, FailedQueueAddress, Status, callback);
            }
        }
    }
}