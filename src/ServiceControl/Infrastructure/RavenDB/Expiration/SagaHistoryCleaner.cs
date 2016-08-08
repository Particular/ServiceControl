namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Diagnostics;
    using System.Threading;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public static class SagaHistoryCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(SagaHistoryCleaner));

        public static void Clean(int deletionBatchSize, IDocumentStore store, DateTime expiryThreshold, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();

            var query = new IndexQuery
            {
                Start = 0,
                DisableCaching = true,
                Cutoff = SystemTime.UtcNow,
                PageSize = deletionBatchSize,
                Query = $"LastModified:[* TO {expiryThreshold.Ticks}]",
                FieldsToFetch = new[]
                {
                    "__document_id",
                },
                SortedFields = new[]
                {
                    new SortedField("LastModified")
                    {
                        Field = "LastModified",
                        Descending = false
                    }
                }
            };

            var indexName = new ExpirySagaAuditIndex().IndexName;
            QueryHeaderInformation _;
            var deletionCount = 0;

            logger.Info("Batching deletion of sagahistory documents.");

            using (var ie = store.DatabaseCommands.StreamQuery(indexName, query, out _))
            {
                while (ie.MoveNext())
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    var doc = ie.Current;
                    var id = doc.Value<string>("__document_id");
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    store.DatabaseCommands.Delete(id, null);
                    deletionCount++;

                    if (deletionCount >= deletionBatchSize)
                    {
                        break;
                    }
                }
            }

            logger.Info("Batching deletion of sagahistory documents completed.");

            if (deletionCount == 0)
            {
                logger.Info("No expired sagahistory documents found");
            }
            else
            {
                logger.InfoFormat("Deleted {0} expired sagahistory documents. Batch execution took {1}ms", deletionCount, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}