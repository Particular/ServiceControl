namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using NServiceBus.IdGeneration;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;
    using Raven.Database.Util;
    using ServiceControl.MessageFailures;

    public class Retryer
    {
        const int PageSize = 1000;

        public IDocumentStore Store { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }

        public void StartRetryForIndex<TIndex>(Expression<Func<MessageFailureHistory, bool>> configure = null) where TIndex : AbstractIndexCreationTask, new()
        {
            var indexName = new TIndex().IndexName;
            StartRetryForIndex(indexName, configure);
        }

        void StartRetryForIndex(string indexName, Expression<Func<MessageFailureHistory, bool>> configure)
        {
            Task.Factory.StartNew(() => CreateAndStageRetriesForIndex(indexName, configure));
        }

        void CreateAndStageRetriesForIndex(string indexName, Expression<Func<MessageFailureHistory, bool>> configure)
        {
            using (var session = Store.OpenSession())
            {
                var qry = session.Query<MessageFailureHistory>(indexName);

                if (configure != null)
                {
                    qry = qry.Where(configure);
                }

                var page = 0;
                var skippedResults = 0;

                while (true)
                {
                    RavenQueryStatistics stats;
                    var ids = qry.Statistics(out stats)
                                .Skip(page * PageSize + skippedResults)
                                .Take(PageSize)
                                .Select(x => x.UniqueMessageId)
                                .ToArray();

                    if (!ids.Any())
                    {
                        break;
                    }

                    StageRetryByUniqueMessageIds(ids);

                    page += 1;
                    skippedResults = stats.SkippedResults;
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
    }
}
