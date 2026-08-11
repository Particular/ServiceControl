namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Auth;
    using MessageFailures;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Persistence;

    class RetriesGateway
    {
        public RetriesGateway(IRetryBatchStore store, RetryingManager operationManager, ILogger<RetriesGateway> logger)
        {
            this.store = store;
            this.operationManager = operationManager;
            this.logger = logger;
        }

        public async Task StartRetryForSingleMessage(string uniqueMessageId, AuditUser? initiatedBy = null, string operationId = null, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Retrying a single message {UniqueMessageId}", uniqueMessageId);

            var requestId = uniqueMessageId;
            var retryType = RetryType.SingleMessage;
            var numberOfMessages = 1;

            await operationManager.Preparing(requestId, retryType, numberOfMessages, cancellationToken);
            await AssignMessagesToBatch(requestId, retryType, new[] { uniqueMessageId }, DateTime.UtcNow, cancellationToken, initiatedBy: initiatedBy, operationId: operationId);
            await operationManager.PreparedBatch(requestId, retryType, numberOfMessages, cancellationToken);
        }

        public async Task StartRetryForMessageSelection(string[] uniqueMessageIds, AuditUser? initiatedBy = null, string operationId = null, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Retrying a selection of {MessageCount} messages", uniqueMessageIds.Length);

            var requestId = DeterministicGuid.MakeId(string.Join(string.Empty, uniqueMessageIds)).ToString();
            var retryType = RetryType.MultipleMessages;
            var numberOfMessages = uniqueMessageIds.Length;

            await operationManager.Preparing(requestId, retryType, numberOfMessages, cancellationToken);
            await AssignMessagesToBatch(requestId, retryType, uniqueMessageIds, DateTime.UtcNow, cancellationToken, initiatedBy: initiatedBy, operationId: operationId);
            await operationManager.PreparedBatch(requestId, retryType, numberOfMessages, cancellationToken);
        }

        async Task AssignMessagesToBatch(string requestId, RetryType retryType, string[] messageIds, DateTime startTime, CancellationToken cancellationToken, DateTime? last = null, string originator = null, string batchName = null, string classifier = null, AuditUser? initiatedBy = null, string operationId = null)
        {
            if (messageIds == null || !messageIds.Any())
            {
                logger.LogInformation("Batch '{BatchName}' contains no messages", batchName);
                return;
            }

            var failedMessageRetryIds = messageIds.ToArray();

            var batchId = await store.CreateBatch(RetryDocumentManager.RetrySessionId, requestId, retryType, failedMessageRetryIds, originator, startTime, last, batchName, classifier, initiatedBy?.Id, initiatedBy?.Name, operationId, cancellationToken);

            logger.LogInformation("Created Batch '{BatchDocumentId}' with {BatchMessageCount} messages for '{BatchName}'", batchId, messageIds.Length, batchName);

            await store.AssignMessagesToBatch(batchId, messageIds, cancellationToken);

            await MoveBatchToStaging(batchId, cancellationToken);

            logger.LogInformation("Moved Batch '{BatchDocumentId}' to Staging", batchId);
        }

        // Needs to be overridable by a test
        protected virtual Task MoveBatchToStaging(string batchId, CancellationToken cancellationToken = default) => store.MoveBatchToStaging(batchId, cancellationToken);


        public async Task<bool> ProcessNextBulkRetry(CancellationToken cancellationToken = default)  // Invoked from BulkRetryBatchCreationHostedService in schedule
        {
            if (!bulkRequests.TryDequeue(out var request))
            {
                return false;
            }

            await ProcessRequest(request, cancellationToken);
            return true;
        }

        async Task ProcessRequest(BulkRetryRequest request, CancellationToken cancellationToken)
        {
            var (batches, latestAttempt) = await request.GetRequestedBatches(store, cancellationToken);
            var totalMessages = batches.Sum(b => b.Length);

            if (!operationManager.IsOperationInProgressFor(request.RequestId, request.RetryType) && totalMessages > 0)
            {
                var numberOfMessagesAdded = 0;

                await operationManager.Preparing(request.RequestId, request.RetryType, totalMessages, cancellationToken);

                for (var i = 0; i < batches.Count; i++)
                {
                    await AssignMessagesToBatch(request.RequestId, request.RetryType, batches[i], request.StartTime, cancellationToken, latestAttempt, request.Originator, GetBatchName(i + 1, batches.Count, request.Originator), request.Classifier, request.InitiatedBy, request.OperationId);
                    numberOfMessagesAdded += batches[i].Length;

                    await operationManager.PreparedBatch(request.RequestId, request.RetryType, numberOfMessagesAdded, cancellationToken);
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

        public void StartRetryForAllMessages(AuditUser? initiatedBy = null, string operationId = null)
        {
            var item = new RetryForAllMessages(initiatedBy, operationId);
            logger.LogInformation("Enqueuing index based bulk retry '{Item}'", item);
            bulkRequests.Enqueue(item);
        }

        public void StartRetryForEndpoint(string endpoint, AuditUser? initiatedBy = null, string operationId = null)
        {
            var item = new RetryForEndpoint(endpoint, initiatedBy, operationId);
            logger.LogInformation("Enqueuing index based bulk retry '{Item}'", item);
            bulkRequests.Enqueue(item);
        }

        public void StartRetryForFailedQueueAddress(string failedQueueAddress, FailedMessageStatus status, AuditUser? initiatedBy = null, string operationId = null)
        {
            var item = new RetryForFailedQueueAddress(failedQueueAddress, status, initiatedBy, operationId);
            logger.LogInformation("Enqueuing index based bulk retry '{Item}'", item);
            bulkRequests.Enqueue(item);
        }

        public void EnqueueRetryForFailureGroup(RetryForFailureGroup item)
        {
            logger.LogInformation("Enqueuing index based bulk retry '{Item}'", item);
            bulkRequests.Enqueue(item);
        }

        readonly IRetryBatchStore store;
        readonly RetryingManager operationManager;
        readonly ConcurrentQueue<BulkRetryRequest> bulkRequests = new ConcurrentQueue<BulkRetryRequest>();
        const int BatchSize = 1000;

        readonly ILogger<RetriesGateway> logger;

        public abstract class BulkRetryRequest
        {
            public string RequestId { get; }
            public RetryType RetryType { get; }
            public string Originator { get; }
            public string Classifier { get; }
            public DateTime StartTime { get; }
            public AuditUser? InitiatedBy { get; }
            public string OperationId { get; }

            public BulkRetryRequest(
                string requestId,
                RetryType retryType,
                DateTime startTime,
                string originator,
                AuditUser? initiatedBy = null,
                string operationId = null
                )
            {
                RequestId = requestId;
                RetryType = retryType;
                Originator = originator;
                StartTime = startTime;
                InitiatedBy = initiatedBy;
                OperationId = operationId;
            }

            protected abstract Task Invoke(IRetryBatchStore store, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default);

            public async Task<Tuple<List<string[]>, DateTime>> GetRequestedBatches(IRetryBatchStore store, CancellationToken cancellationToken = default)
            {
                var response = new List<string[]>();
                var currentBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var latestAttempt = DateTime.MinValue;

                Task Process(string uniqueMessageId, DateTime latestTimeOfFailure, CancellationToken cancellationToken)
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

                await Invoke(store, Process, cancellationToken);

                if (currentBatch.Count > 0)
                {
                    response.Add(currentBatch.ToArray());
                }


                return Tuple.Create(response, latestAttempt);
            }
        }

        class RetryForAllMessages : BulkRetryRequest
        {
            public RetryForAllMessages(AuditUser? initiatedBy = null, string operationId = null) : base(requestId: "All", RetryType.All, DateTime.UtcNow, "all messages", initiatedBy, operationId)
            {
            }

            protected override Task Invoke(IRetryBatchStore store, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default)
            {
                return store.ForEachUnresolvedMessage(callback, cancellationToken);
            }
        }

        class RetryForEndpoint : BulkRetryRequest
        {
            public string Endpoint { get; }

            public RetryForEndpoint(string endpoint, AuditUser? initiatedBy = null, string operationId = null) : base(requestId: endpoint, RetryType.AllForEndpoint, DateTime.UtcNow, originator: $"all messages for endpoint {endpoint}", initiatedBy, operationId)
            {
                Endpoint = endpoint;
            }

            protected override Task Invoke(IRetryBatchStore store, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default)
            {
                return store.ForEachUnresolvedMessageForEndpoint(Endpoint, callback, cancellationToken);
            }
        }

        public sealed class RetryForFailureGroup : BulkRetryRequest
        {
            public string GroupId { get; }
            public string GroupTitle { get; }
            public string GroupType { get; }

            public RetryForFailureGroup(string groupId, string groupTitle, string groupType, DateTime started, AuditUser? initiatedBy = null, string operationId = null) : base(requestId: groupId, RetryType.FailureGroup, started, originator: groupTitle, initiatedBy, operationId)
            {
                GroupId = groupId;
                GroupType = groupType;
                GroupTitle = groupTitle;
            }

            protected override Task Invoke(IRetryBatchStore store, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default)
            {
                return store.ForEachUnresolvedMessageInGroup(GroupId, callback, cancellationToken);
            }
        }

        class RetryForFailedQueueAddress : BulkRetryRequest
        {
            public string FailedQueueAddress { get; }
            public FailedMessageStatus Status { get; }


            public RetryForFailedQueueAddress(
                string failedQueueAddress,
                FailedMessageStatus status,
                AuditUser? initiatedBy = null,
                string operationId = null
                ) : base(requestId: failedQueueAddress, RetryType.ByQueueAddress, DateTime.UtcNow, originator: $"all messages for failed queue address '{failedQueueAddress}'", initiatedBy, operationId)
            {
                FailedQueueAddress = failedQueueAddress;
                Status = status;
            }

            protected override Task Invoke(IRetryBatchStore store, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default)
            {
                return store.ForEachMessageForQueueAddress(FailedQueueAddress, Status, callback, cancellationToken);
            }
        }
    }
}