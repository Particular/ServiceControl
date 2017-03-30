namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using NServiceBus.Logging;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;
    using ServiceControl.MessageFailures;


    public class RetriesGateway
    {
        const int BatchSize = 1000;

        private IDocumentStore store;
        private RetryDocumentManager retryDocumentManager;
        public RetryingManager OperationManager { get; set; }
        private ConcurrentQueue<IBulkRetryRequest> bulkRequests = new ConcurrentQueue<IBulkRetryRequest>();
        public RetriesGateway(IDocumentStore store, RetryDocumentManager documentManager)
        {
            this.store = store;
            retryDocumentManager = documentManager;
        }

        interface IBulkRetryRequest
        {
            string RequestId { get; }
            RetryType RetryType { get; }
            string Originator { get; set; }
            string Classifier { get; set; }
            DateTime StartTime { get; set; }
            IEnumerator<StreamResult<FailedMessage>> GetDocuments(IDocumentSession session);
        }

        class IndexBasedBulkRetryRequest<TType, TIndex> : IBulkRetryRequest
            where TIndex : AbstractIndexCreationTask, new()
            where TType : IHaveStatus
        {
            Expression<Func<TType, bool>> filter;

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

            public IEnumerator<StreamResult<FailedMessage>> GetDocuments(IDocumentSession session)
            {
                var query = session.Query<TType, TIndex>();

                query = query.Where(d => d.Status == FailedMessageStatus.Unresolved);

                if (filter != null)
                {
                    query = query.Where(filter);
                }

                return session.Advanced.Stream(query.As<FailedMessage>());
            }
        }

        IList<string[]> GetRequestedBatches(IBulkRetryRequest request, out DateTime latestAttempt)
        {
            var response = new List<string[]>();
            var currentBatch = new List<string>(BatchSize);
            latestAttempt = DateTime.MinValue;

            using (var session = store.OpenSession())
            using (var stream = request.GetDocuments(session))
            {
                while (stream.MoveNext())
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

            return response;
        }

        public void StartRetryForIndex<TType, TIndex>(string requestId, RetryType retryType, DateTime startTime, Expression<Func<TType, bool>> filter = null, string originator = null, string classifier = null)
            where TIndex : AbstractIndexCreationTask, new()
            where TType : IHaveStatus
        {
            log.InfoFormat("Enqueuing index based bulk retry '{0}'", originator);

            var request = new IndexBasedBulkRetryRequest<TType, TIndex>(requestId, retryType, originator, classifier, startTime, filter);

            bulkRequests.Enqueue(request);
        }

        public void StageRetryByUniqueMessageIds(string requestId, RetryType retryType, string[] messageIds, DateTime startTime, DateTime? last = null, string originator = null, string batchName = null, string classifier = null)
        {
            if (messageIds == null || !messageIds.Any())
            {
                log.DebugFormat("Batch '{0}' contains no messages", batchName);
                return;
            }

            var batchDocumentId = retryDocumentManager.CreateBatchDocument(requestId, retryType, messageIds.Length, originator, startTime, last, batchName, classifier);

            log.InfoFormat("Created Batch '{0}' with {1} messages for '{2}'", batchDocumentId, messageIds.Length, batchName);

            var retryIds = new string[messageIds.Length];
            var commands = new ICommandData[messageIds.Length];
            for (var i = 0; i < messageIds.Length; i++)
            {
                commands[i] = retryDocumentManager.CreateFailedMessageRetryDocument(batchDocumentId, messageIds[i]);
                retryIds[i] = commands[i].Key;
            }

            store.DatabaseCommands.Batch(commands);

            retryDocumentManager.MoveBatchToStaging(batchDocumentId, retryIds);
            log.InfoFormat("Moved Batch '{0}' to Staging", batchDocumentId);
        }

        internal bool ProcessNextBulkRetry()
        {
            IBulkRetryRequest request;
            if (!bulkRequests.TryDequeue(out request))
            {
                return false;
            }
            
            ProcessRequest(request);
            return true;
        }

        void ProcessRequest(IBulkRetryRequest request)
        {
            DateTime latestAttempt;
            var batches = GetRequestedBatches(request, out latestAttempt);
            var totalMessages = batches.Sum(b => b.Length);

            if (!OperationManager.IsOperationInProgressFor(request.RequestId, request.RetryType) && totalMessages > 0)
            {
                var numberOfMessagesAdded = 0;

                OperationManager.Prepairing(request.RequestId, request.RetryType, totalMessages);

                for (var i = 0; i < batches.Count; i++)
                {
                    StageRetryByUniqueMessageIds(request.RequestId, request.RetryType, batches[i], request.StartTime, latestAttempt, request.Originator, GetBatchName(i + 1, batches.Count, request.Originator), request.Classifier);
                    numberOfMessagesAdded += batches[i].Length;

                    OperationManager.PreparedBatch(request.RequestId, request.RetryType, numberOfMessagesAdded);
                }
            }
        }

        private string GetBatchName(int pageNum, int totalPages, string context)
        {
            if (context == null)
                return null;
            return $"'{context}' batch {pageNum} of {totalPages}";
        }

        static ILog log = LogManager.GetLogger(typeof(RetriesGateway));
    }
}