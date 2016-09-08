namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;
    using ServiceControl.MessageFailures;

    public class RetriesGateway
    {
        const int BatchSize = 1000;

        public IDocumentStore Store { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }

        ConcurrentQueue<IBulkRetryRequest> _bulkRequests = new ConcurrentQueue<IBulkRetryRequest>();

        interface IBulkRetryRequest
        {
            IEnumerator<StreamResult<FailedMessage>> GetDocuments(IDocumentSession session);
            string GetBatchName(int pageNum, int totalPages);
        }

        class IndexBasedBulkRetryRequest<TType, TIndex> : IBulkRetryRequest
            where TIndex : AbstractIndexCreationTask, new()
            where TType : IHaveStatus
        {
            string context;
            Expression<Func<TType, bool>> filter;

            public IndexBasedBulkRetryRequest(string context, Expression<Func<TType, bool>> filter)
            {
                this.context = context;
                this.filter = filter;
            }

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

            public string GetBatchName(int pageNum, int totalPages)
            {
                if (context == null)
                    return null;
                return $"Retry '{context}' batch {pageNum} of {totalPages}";
            }
        }

        IList<string[]> GetRequestedBatches(IBulkRetryRequest request)
        {
            var batches = new List<string[]>();
            var currentBatch = new List<string>(BatchSize);

            using (var session = Store.OpenSession())
            using (var stream = request.GetDocuments(session))
            {
                while (stream.MoveNext())
                {
                    currentBatch.Add(stream.Current.Document.UniqueMessageId);
                    if (currentBatch.Count == BatchSize)
                    {
                        batches.Add(currentBatch.ToArray());
                        currentBatch.Clear();
                    }
                }

                if (currentBatch.Any())
                {
                    batches.Add(currentBatch.ToArray());
                }
            }

            return batches;
        }

        public void StartRetryForIndex<TType, TIndex>(Expression<Func<TType, bool>> filter = null, string context = null)
            where TIndex : AbstractIndexCreationTask, new()
            where TType : IHaveStatus
        {
            log.InfoFormat("Enqueuing index based bulk retry '{0}'", context);

            var request = new IndexBasedBulkRetryRequest<TType, TIndex>(context, filter);

            _bulkRequests.Enqueue(request);
        }

        public void StageRetryByUniqueMessageIds(string[] messageIds, string context = null)
        {
            if (messageIds == null || !messageIds.Any())
            {
                log.DebugFormat("Context '{0}' contains no messages", context);
                return;
            }

            var batchDocumentId = RetryDocumentManager.CreateBatchDocument(context);

            log.InfoFormat("Created Batch '{0}' with {1} messages for context '{2}'", batchDocumentId, messageIds.Length, context);

            var retryIds = new ConcurrentDictionary<string, object>();
            Parallel.ForEach(
                messageIds,
                id => retryIds.TryAdd(RetryDocumentManager.CreateFailedMessageRetryDocument(batchDocumentId, id), null));

            RetryDocumentManager.MoveBatchToStaging(batchDocumentId, retryIds.Keys.ToArray());
            log.InfoFormat("Moved Batch '{0}' to Staging", batchDocumentId);
        }

        internal bool ProcessNextBulkRetry()
        {
            IBulkRetryRequest request;
            if (!_bulkRequests.TryDequeue(out request))
            {
                return false;
            }

            ProcessRequest(request);
            return true;
        }

        void ProcessRequest(IBulkRetryRequest request)
        {
            var batches = GetRequestedBatches(request);

            for (var i = 0; i < batches.Count; i++)
            {
                StageRetryByUniqueMessageIds(batches[i], request.GetBatchName(i + 1, batches.Count));
            }
        }

        static ILog log = LogManager.GetLogger(typeof(RetriesGateway));
    }
}