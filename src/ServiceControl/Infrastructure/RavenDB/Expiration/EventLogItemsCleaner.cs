namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using NServiceBus.Logging;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Database;

    public static class EventLogItemsCleaner
    {
        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold)
        {
            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            try
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
                        "__document_id"
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
                var indexName = new ExpiryEventLogItemsIndex().IndexName;
                database.Query(indexName, query, database.WorkContext.CancellationToken,
                    doc =>
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
                    });
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }

            var deletionCount = 0;

            Chunker.ExecuteInChunks(items.Count, (s, e) =>
            {
                logger.InfoFormat("Batching deletion of {0}-{1} eventlogitem documents.", s, e);
                var results = database.Batch(items.GetRange(s, e - s + 1), CancellationToken.None);
                logger.InfoFormat("Batching deletion of {0}-{1} eventlogitem documents completed.", s, e);

                deletionCount += results.Count(x => x.Deleted == true);
            });

            if (deletionCount == 0)
            {
                logger.Info("No expired eventlogitem documents found");
            }
            else
            {
                logger.InfoFormat("Deleted {0} expired eventlogitem documents. Batch execution took {1}ms", deletionCount, stopwatch.ElapsedMilliseconds);
            }
        }

        static ILog logger = LogManager.GetLogger(typeof(EventLogItemsCleaner));
    }
}