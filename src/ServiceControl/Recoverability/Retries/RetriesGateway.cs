namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;
    using Raven.Database.Util;
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
                return string.Format("Retry '{0}' batch {1} of {2}", context, pageNum, totalPages);
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
            var request = new IndexBasedBulkRetryRequest<TType, TIndex>(context, filter);

            _bulkRequests.Enqueue(request);
        }

        public void StageRetryByUniqueMessageIds(string[] messageIds, string context = null)
        {
            if (messageIds == null || !messageIds.Any())
            {
                return;
            }

            var batchDocumentId = RetryDocumentManager.CreateBatchDocument(context);

            var retryIds = new ConcurrentSet<string>();
            Parallel.ForEach(
                messageIds,
                id => retryIds.Add(RetryDocumentManager.CreateFailedMessageRetryDocument(batchDocumentId, id)));

            RetryDocumentManager.MoveBatchToStaging(batchDocumentId, retryIds.ToArray());
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
    }
}