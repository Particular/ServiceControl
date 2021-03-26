namespace ServiceControl.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Infrastructure.RavenDB;
    using NServiceBus.Logging;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Database;

    public static class SagaHistoryCleaner
    {
        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            var indexName = new ExpirySagaAuditIndex().IndexName;

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

                database.Query(indexName, query, (doc, commands) =>
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
                    },
                    items, token);
            }
            catch (IndexDisabledException ex)
            {
                logger.Error($"Unable to cleanup saga history. The index ${indexName} was disabled.", ex);
                return;
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
                    logger.Debug($"Batching deletion of {s}-{e} saga history documents.");
                }

                var results = db.Batch(itemsForBatch.GetRange(s, e - s + 1), CancellationToken.None);
                if (logger.IsDebugEnabled)
                {
                    logger.Debug($"Batching deletion of {s}-{e} saga history documents completed.");
                }

                return results.Count(x => x.Deleted == true);
            }, items, database, token);

            if (deletionCount == 0)
            {
                if (logger.IsDebugEnabled)
                {
                    logger.Debug("No expired saga history documents found");
                }
            }
            else
            {
                if (logger.IsDebugEnabled)
                {
                    logger.Debug($"Deleted {deletionCount} expired saga history documents. Batch execution took {stopwatch.ElapsedMilliseconds} ms");
                }
            }
        }

        static ILog logger = LogManager.GetLogger(typeof(SagaHistoryCleaner));
    }
}
