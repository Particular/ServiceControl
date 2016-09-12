namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using System.Linq;

    public static class SagaHistoryCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(SagaHistoryCleaner));

        public static void Clean(int deletionBatchSize, IDocumentStore store, DateTime expiryThreshold, CancellationToken token)
        {
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

            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            var indexName = new ExpirySagaAuditIndex().IndexName;

            logger.Info("Starting clean-up of expired sagahistory documents.");

            var qResults = store.DatabaseCommands.Query(indexName, query);

            foreach (var doc in qResults.Results)
            {
                var id = doc.Value<string>("__document_id");
                if (string.IsNullOrEmpty(id))
                {
                    return;
                }

                items.Add(new DeleteCommandData
                {
                    Key = id
                });
            }
            logger.Info($"Query for expired sagahistory documents took {stopwatch.ElapsedMilliseconds}ms.");

            stopwatch.Restart();

            var deletionCount = 0;

            Chunker.ExecuteInChunks(items.Count, (s, e, t) =>
            {
                var results = store.DatabaseCommands.Batch(items.GetRange(s, e - s + 1));
                logger.Info($"Batching deletion of {t}/{items.Count} sagahistory documents completed.");

                deletionCount += results.Count(x => x.Deleted == true);
            }, token);

            if (deletionCount == 0)
            {
                logger.Info("No expired sagahistory documents found");
            }
            else
            {
                logger.Info($"Deleted {deletionCount} sagahistory audit documents in {stopwatch.ElapsedMilliseconds}ms.");
            }
        }
    }
}