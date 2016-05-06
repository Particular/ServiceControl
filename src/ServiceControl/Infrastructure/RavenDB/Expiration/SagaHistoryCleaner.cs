﻿namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Database;

    public static class SagaHistoryCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(SagaHistoryCleaner));

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
                var docs = database.Queries.Query(indexName, query, database.WorkContext.CancellationToken).Results;
                foreach (var doc in docs)
                {
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
            catch (OperationCanceledException)
            {
                //Ignore
            }

            var deletionCount = 0;

            Chunker.ExecuteInChunks(items.Count, (s, e) =>
            {
                logger.InfoFormat("Batching deletion of {0}-{1} sagahistory documents.", s, e);
                var results = database.Batch(items.GetRange(s, e - s + 1), CancellationToken.None);
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