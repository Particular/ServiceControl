namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
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

        public void StartRetryForIndex<TType, TIndex>(Expression<Func<TType, bool>> filter = null) where TIndex : AbstractIndexCreationTask, new()
        {
            Task.Factory.StartNew(
                () => CreateAndStageRetriesForIndex<TType, TIndex>(filter, cancellationTokenSource.Token),
                cancellationTokenSource.Token);
        }

        void CreateAndStageRetriesForIndex<TType, TIndex>(Expression<Func<TType, bool>> filter, CancellationToken token) where TIndex : AbstractIndexCreationTask, new()
        {
            using (var session = Store.OpenSession())
            {
                var query = session.Query<TType, TIndex>();

                if (filter != null)
                {
                    query = query.Where(filter);
                }

                var currentBatch = new List<string>();

                using (var stream = session.Advanced.Stream(query.As<FailedMessage>()))
                {
                    while (stream.MoveNext() && !token.IsCancellationRequested)
                    {
                        currentBatch.Add(stream.Current.Document.UniqueMessageId);
                        if (currentBatch.Count == BatchSize)
                        {
                            StageRetryByUniqueMessageIds(currentBatch.ToArray());
                            currentBatch.Clear();
                        }
                    }
                }

                if (currentBatch.Any())
                {
                    StageRetryByUniqueMessageIds(currentBatch.ToArray());
                }
            }
        }

        public void StageRetryByUniqueMessageIds(string[] messageIds)
        {
            if (messageIds == null || !messageIds.Any())
            {
                return;
            }

            var batchDocumentId = RetryDocumentManager.CreateBatchDocument();

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