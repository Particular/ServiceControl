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
            {
                Logger.Info("Retry group: Loading documents for request");
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
            }

            Logger.InfoFormat("Retry group: Loaded {0} new messages to retry", batches.Count);
            return batches;
        }

        public void StartRetryForIndex<TType, TIndex>(Expression<Func<TType, bool>> filter = null, string context = null)
            where TIndex : AbstractIndexCreationTask, new()
            where TType : IHaveStatus
        {
            Logger.InfoFormat("Retry group: Starting retry for index with context: {0}", context ?? "null");
            var request = new IndexBasedBulkRetryRequest<TType, TIndex>(context, filter);

            Logger.Info("Retry group: Enqueuing IndexBasedBulkRetryRequest");
            _bulkRequests.Enqueue(request);
        }

        public void StageRetryByUniqueMessageIds(string[] messageIds, string context = null)
        {
            if (messageIds == null || !messageIds.Any())
            {
            Logger.Info("Retry group: No messages to retry");
                return;
            }

            Logger.InfoFormat("Retry group: Retrying messages '{0}'", string.Join(", ", messageIds));

            var batchDocumentId = RetryDocumentManager.CreateBatchDocument(context);
            Logger.InfoFormat("Retry group: Retrying batch document with Id {0}", batchDocumentId);

            var retryIds = new ConcurrentSet<string>();
            Parallel.ForEach(
                messageIds,
                id => retryIds.Add(RetryDocumentManager.CreateFailedMessageRetryDocument(batchDocumentId, id)));

            RetryDocumentManager.MoveBatchToStaging(batchDocumentId, retryIds.ToArray());
        }

        internal bool ProcessNextBulkRetry()
        {
            Logger.Info("Retry group: Processing next bulk retry");
            IBulkRetryRequest request;
            if (!_bulkRequests.TryDequeue(out request))
            {
                return false;
            }

            Logger.Info("Retry group: Bulk retry found");
            ProcessRequest(request);
            return true;
        }

        void ProcessRequest(IBulkRetryRequest request)
        {
            Logger.Info("Retry group: Processing IBulkRetryRequest");
            var batches = GetRequestedBatches(request);

            for (var i = 0; i < batches.Count; i++)
            {
                StageRetryByUniqueMessageIds(batches[i], request.GetBatchName(i + 1, batches.Count));
            }

            Logger.Info("Retry group: Finished processing IBulkRetryRequest");
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RetriesGateway));
    }
}