namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using MessageFailures;
    using NServiceBus.Logging;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Util;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;

    public class RetriesGateway
    {
        public RetriesGateway(IDocumentStore store, RetryDocumentManager documentManager)
        {
            this.store = store;
            retryDocumentManager = documentManager;
        }

        public RetryingManager OperationManager { get; set; }

        async Task<Tuple<List<string[]>, DateTime>> GetRequestedBatches(IBulkRetryRequest request)
        {
            var response = new List<string[]>();
            var currentBatch = new List<string>(BatchSize);
            var latestAttempt = DateTime.MinValue;

            using (var session = store.OpenAsyncSession())
            using (var stream = await request.GetDocuments(session).ConfigureAwait(false))
            {
                while (await stream.MoveNextAsync())
                {
                    var current = stream.Current.Document;
                    currentBatch.Add(current.UniqueMessageId);

                    if (currentBatch.Count == BatchSize)
                    {
                        response.Add(currentBatch.ToArray());

                        currentBatch.Clear();
                    }

                    var lastDocumentAttempt = current.ProcessingAttempts.Select(x => x.FailureDetails.TimeOfFailure).Max();
                    if (lastDocumentAttempt > latestAttempt)
                    {
                        latestAttempt = lastDocumentAttempt;
                    }
                }

                if (currentBatch.Any())
                {
                    response.Add(currentBatch.ToArray());
                }
            }

            return Tuple.Create(response, latestAttempt);
        }

        public void StartRetryForIndex<TType, TIndex>(string requestId, RetryType retryType, DateTime startTime, Expression<Func<TType, bool>> filter = null, string originator = null, string classifier = null)
            where TIndex : AbstractIndexCreationTask, new()
            where TType : IHaveStatus
        {
            log.InfoFormat("Enqueuing index based bulk retry '{0}'", originator);

            var request = new IndexBasedBulkRetryRequest<TType, TIndex>(requestId, retryType, originator, classifier, startTime, filter);

            bulkRequests.Enqueue(request);
        }

        public async Task StartRetryForSingleMessage(string uniqueMessageId)
        {
            log.InfoFormat("Retrying a single message {0}", uniqueMessageId);

            var requestId = uniqueMessageId;
            var retryType = RetryType.SingleMessage;
            var numberOfMessages = 1;

            await OperationManager.Prepairing(requestId, retryType, numberOfMessages)
                .ConfigureAwait(false);
            await StageRetryByUniqueMessageIds(requestId, retryType, new[] {uniqueMessageId}, DateTime.UtcNow)
                .ConfigureAwait(false);
            await OperationManager.PreparedBatch(requestId, retryType, numberOfMessages)
                .ConfigureAwait(false);
        }

        public async Task StartRetryForMessageSelection(string[] uniqueMessageIds)
        {
            log.InfoFormat("Retrying a selection of {0} messages", uniqueMessageIds.Length);

            var requestId = Guid.NewGuid().ToString();
            var retryType = RetryType.MultipleMessages;
            var numberOfMessages = uniqueMessageIds.Length;

            await OperationManager.Prepairing(requestId, retryType, numberOfMessages)
                .ConfigureAwait(false);
            await StageRetryByUniqueMessageIds(requestId, retryType, uniqueMessageIds, DateTime.UtcNow)
                .ConfigureAwait(false);
            await OperationManager.PreparedBatch(requestId, retryType, numberOfMessages)
                .ConfigureAwait(false);
        }

        private async Task StageRetryByUniqueMessageIds(string requestId, RetryType retryType, string[] messageIds, DateTime startTime, DateTime? last = null, string originator = null, string batchName = null, string classifier = null)
        {
            if (messageIds == null || !messageIds.Any())
            {
                log.DebugFormat("Batch '{0}' contains no messages", batchName);
                return;
            }

            var failedMessageRetryIds = messageIds.Select(FailedMessageRetry.MakeDocumentId).ToArray();

            var batchDocumentId = await retryDocumentManager.CreateBatchDocument(requestId, retryType, failedMessageRetryIds, originator, startTime, last, batchName, classifier)
                .ConfigureAwait(false);

            log.InfoFormat("Created Batch '{0}' with {1} messages for '{2}'", batchDocumentId, messageIds.Length, batchName);

            var commands = new ICommandData[messageIds.Length];
            for (var i = 0; i < messageIds.Length; i++)
            {
                commands[i] = retryDocumentManager.CreateFailedMessageRetryDocument(batchDocumentId, messageIds[i]);
            }

            await store.AsyncDatabaseCommands.BatchAsync(commands)
                .ConfigureAwait(false);

            await retryDocumentManager.MoveBatchToStaging(batchDocumentId).ConfigureAwait(false);

            log.InfoFormat("Moved Batch '{0}' to Staging", batchDocumentId);
        }

        internal async Task<bool> ProcessNextBulkRetry()
        {
            if (!bulkRequests.TryDequeue(out var request))
            {
                return false;
            }

            await ProcessRequest(request).ConfigureAwait(false);
            return true;
        }

        async Task ProcessRequest(IBulkRetryRequest request)
        {
            var batchesWithLastAttempt = await GetRequestedBatches(request).ConfigureAwait(false);
            var batches = batchesWithLastAttempt.Item1;
            var latestAttempt = batchesWithLastAttempt.Item2;
            var totalMessages = batches.Sum(b => b.Length);

            if (!OperationManager.IsOperationInProgressFor(request.RequestId, request.RetryType) && totalMessages > 0)
            {
                var numberOfMessagesAdded = 0;

                await OperationManager.Prepairing(request.RequestId, request.RetryType, totalMessages)
                    .ConfigureAwait(false);

                for (var i = 0; i < batches.Count; i++)
                {
                    await StageRetryByUniqueMessageIds(request.RequestId, request.RetryType, batches[i], request.StartTime, latestAttempt, request.Originator, GetBatchName(i + 1, batches.Count, request.Originator), request.Classifier)
                        .ConfigureAwait(false);
                    numberOfMessagesAdded += batches[i].Length;

                    await OperationManager.PreparedBatch(request.RequestId, request.RetryType, numberOfMessagesAdded)
                        .ConfigureAwait(false);
                }
            }
        }

        private string GetBatchName(int pageNum, int totalPages, string context)
        {
            if (context == null)
            {
                return null;
            }

            return $"'{context}' batch {pageNum} of {totalPages}";
        }

        private IDocumentStore store;
        private RetryDocumentManager retryDocumentManager;
        private ConcurrentQueue<IBulkRetryRequest> bulkRequests = new ConcurrentQueue<IBulkRetryRequest>();
        const int BatchSize = 1000;

        static ILog log = LogManager.GetLogger(typeof(RetriesGateway));

        interface IBulkRetryRequest
        {
            string RequestId { get; }
            RetryType RetryType { get; }
            string Originator { get; set; }
            string Classifier { get; set; }
            DateTime StartTime { get; set; }
            Task<IAsyncEnumerator<StreamResult<FailedMessage>>> GetDocuments(IAsyncDocumentSession session);
        }

        class IndexBasedBulkRetryRequest<TType, TIndex> : IBulkRetryRequest
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

            public Task<IAsyncEnumerator<StreamResult<FailedMessage>>> GetDocuments(IAsyncDocumentSession session)
            {
                var query = session.Query<TType, TIndex>();

                query = query.Where(d => d.Status == FailedMessageStatus.Unresolved);

                if (filter != null)
                {
                    query = query.Where(filter);
                }

                return session.Advanced.StreamAsync(query.As<FailedMessage>());
            }

            Expression<Func<TType, bool>> filter;
        }
    }
}