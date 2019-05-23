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

    static class EventLogItemsCleaner
    {
        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold, CancellationToken token)
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
                database.Query(indexName, query, token,
                    (doc, commands) =>
                    {
                        var id = doc.Value<string>("__document_id");
                        if (string.IsNullOrEmpty(id))
                        {
                            return;
                        }

                        commands.Add(new DeleteCommandData
                        {
                            Key = id
                        });
                    }, items);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Cleanup operation cancelled");
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            var deletionCount = Chunker.ExecuteInChunks(items.Count, (itemsForBatch, db, s, e) =>
            {
                if (logger.IsDebugEnabled)
                {
                    logger.Debug($"Batching deletion of {s}-{e} event log documents.");
                }

                var results = db.Batch(itemsForBatch.GetRange(s, e - s + 1), CancellationToken.None);

                if (logger.IsDebugEnabled)
                {
                    logger.Debug($"Batching deletion of {s}-{e} event log documents completed.");
                }

                return results.Count(x => x.Deleted == true);
            }, items, database, token);

            if (deletionCount == 0)
            {
                logger.Info("No expired event log documents found");
            }
            else
            {
                logger.Info($"Deleted {deletionCount} expired event log documents. Batch execution took {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        static ILog logger = LogManager.GetLogger(typeof(EventLogItemsCleaner));
    }
}