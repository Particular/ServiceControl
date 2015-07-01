namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.IdGeneration;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;
    using Raven.Database.Util;
    using ServiceControl.MessageFailures;

    public class Retryer
    {
        const int BatchSize = 1000;

        public IDocumentStore Store { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public void StartRetryForIndex<TIndex>(Expression<Func<MessageFailureHistory, bool>> configure = null) where TIndex : AbstractIndexCreationTask, new()
        {
            var indexName = new TIndex().IndexName;
            StartRetryForIndex(indexName, configure);
        }

        void StartRetryForIndex(string indexName, Expression<Func<MessageFailureHistory, bool>> configure)
        {
            Task.Factory.StartNew(
                () => CreateAndStageRetriesForIndex(indexName, configure, cancellationTokenSource.Token)
                , cancellationTokenSource.Token);
        }

        void CreateAndStageRetriesForIndex(string indexName, Expression<Func<MessageFailureHistory, bool>> configure, CancellationToken token)
        {
            using (var session = Store.OpenSession())
            {
                var qry = session.Query<MessageFailureHistory>(indexName);

                if (configure != null)
                {
                    qry = qry.Where(configure);
                }

                var currentBatch = new List<string>();

                using (var stream = session.Advanced.Stream(qry))
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

            var batchDocumentId = RetryBatch.MakeId(CombGuid.Generate().ToString());

            RetryDocumentManager.CreateBatch(batchDocumentId);

            var failureRetryIds = new ConcurrentSet<string>();
            Parallel.ForEach(messageIds, id => failureRetryIds.Add(RetryDocumentManager.MakeFailureRetryDocument(batchDocumentId, id)));

            RetryDocumentManager.MoveBatchToStaging(batchDocumentId, failureRetryIds.ToArray());
        }

        internal void StopProcessingOutstandingBatches()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}
