namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
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

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public void StartRetryForIndex<TType, TIndex>(Expression<Func<TType, bool>> filter = null, string context = null) where TIndex : AbstractIndexCreationTask, new() where TType : IHaveStatus
        {
            Task.Factory.StartNew(
                () => CreateAndStageRetriesForIndex<TType, TIndex>(filter, cancellationTokenSource.Token, context),
                cancellationTokenSource.Token);
        }

        void CreateAndStageRetriesForIndex<TType, TIndex>(Expression<Func<TType, bool>> filter, CancellationToken token, string context) where TIndex : AbstractIndexCreationTask, new() where TType : IHaveStatus
        {
            using (var session = Store.OpenSession())
            {
                var query = session.Query<TType, TIndex>();

                query = query.Where(d=>d.Status == FailedMessageStatus.Unresolved);

                if (filter != null)
                {
                    query = query.Where(filter);
                }

                var currentBatch = new List<string>();
                QueryHeaderInformation info;
                int totalPages;
                var currentPage = 1;
                string batchContext = null;

                using (var stream = session.Advanced.Stream(query.As<FailedMessage>(), out info))
                {
                    totalPages = (int)Math.Ceiling(info.TotalResults/(decimal)BatchSize);
                    while (stream.MoveNext() && !token.IsCancellationRequested)
                    {
                        currentBatch.Add(stream.Current.Document.UniqueMessageId);
                        if (currentBatch.Count == BatchSize)
                        {
                            if (context != null)
                            {
                                batchContext = string.Format("Retry '{0}' batch {1} of {2}", context, currentPage, totalPages);
                            }
                            StageRetryByUniqueMessageIds(currentBatch.ToArray(), batchContext);
                            currentPage += 1;
                            currentBatch.Clear();
                        }
                    }
                }

                if (currentBatch.Any())
                {
                    if (context != null)
                    {
                        batchContext = string.Format("Retry '{0}' batch {1} of {2}", context, currentPage, totalPages);
                    }
                    StageRetryByUniqueMessageIds(currentBatch.ToArray(), batchContext);
                }
            }
        }

        public void StageRetryByUniqueMessageIds(string[] messageIds, string context = null)
        {
            if (messageIds == null || !messageIds.Any())
            {
                return;
            }

            var batchDocumentId = RetryDocumentManager.CreateBatchDocument(context);

            var retryIds = new ConcurrentSet<string>();
            Parallel.ForEach(messageIds, id => retryIds.Add(RetryDocumentManager.CreateFailedMessageRetryDocument(batchDocumentId, id)));

            RetryDocumentManager.MoveBatchToStaging(batchDocumentId, retryIds.ToArray());
        }

        internal void StopProcessingOutstandingBatches()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}