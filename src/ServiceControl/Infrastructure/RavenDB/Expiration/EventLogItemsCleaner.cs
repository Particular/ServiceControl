﻿namespace ServiceControl.Infrastructure.RavenDB.Expiration
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
    using Raven.Abstractions.Exceptions;
    using Raven.Database;

    static class EventLogItemsCleaner
    {
        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            var indexName = new ExpiryEventLogItemsIndex().IndexName;

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
                    items, cancellationToken);
            }
            catch (IndexDisabledException ex)
            {
                logger.Error($"Unable to cleanup event log items. The index ${indexName} was disabled.", ex);
                return;
            }
            catch (OperationCanceledException)
            {
                logger.Info("Cleanup operation cancelled");
                return;
            }

            if (cancellationToken.IsCancellationRequested)
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
            }, items, database, cancellationToken);

            if (deletionCount == 0)
            {
                if (logger.IsDebugEnabled)
                {
                    logger.Debug("No expired event log documents found");
                }
            }
            else
            {
                if (logger.IsDebugEnabled)
                {
                    logger.Debug($"Deleted {deletionCount} expired event log documents. Batch execution took {stopwatch.ElapsedMilliseconds} ms");
                }
            }
        }

        static ILog logger = LogManager.GetLogger(typeof(EventLogItemsCleaner));
    }
}