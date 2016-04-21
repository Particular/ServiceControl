namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public static class SagaHistoryCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(SagaHistoryCleaner));

        public static void Clean(int deletionBatchSize, IDocumentStore store, DateTime expiryThreshold)
        {
            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);

            var query = new IndexQuery
            {
                Start = 0,
                DisableCaching = true,
                Cutoff = SystemTime.UtcNow,
                PageSize = deletionBatchSize,
                Query = string.Format("LastModified:[* TO {0}]", expiryThreshold.Ticks),
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
            using (var ie = store.DatabaseCommands.StreamQuery(indexName, query, out _))
            {
                while (ie.MoveNext())
                {
                    var doc = ie.Current;
                    var id = doc.Value<string>("__document_id");
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    items.Add(new DeleteCommandData
                    {
                        Key = id
                    });
                }
            }

            var deletionCount = 0;

            Chunker.ExecuteInChunks(items.Count, (s, e) =>
            {
                logger.InfoFormat("Batching deletion of {0}-{1} sagahistory documents.", s, e);
                var results = store.DatabaseCommands.Batch(items.GetRange(s, e - s + 1));
                logger.InfoFormat("Batching deletion of {0}-{1} sagahistory documents completed.", s, e);

                deletionCount += results.Count(x => x.Deleted == true);
            });

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