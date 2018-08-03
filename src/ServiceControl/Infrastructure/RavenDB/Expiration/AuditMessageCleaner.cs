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
    using Raven.Json.Linq;

    public static class AuditMessageCleaner
    {
        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold)
        {
            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            var attachments = new List<string>(deletionBatchSize);
            try
            {
                var query = new IndexQuery
                {
                    Start = 0,
                    PageSize = deletionBatchSize,
                    Cutoff = SystemTime.UtcNow,
                    DisableCaching = true,
                    Query = $"ProcessedAt:[* TO {expiryThreshold.Ticks}]",
                    FieldsToFetch = new[]
                    {
                        "__document_id",
                        "BodyUrl"
                    },
                    SortedFields = new[]
                    {
                        new SortedField("ProcessedAt")
                        {
                            Field = "ProcessedAt",
                            Descending = false
                        }
                    }
                };
                var indexName = new ExpiryProcessedMessageIndex().IndexName;
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

                        if (TryGetBodyId(doc, out var bodyId))
                        {
                            attachments.Add(bodyId);
                        }
                    });
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }

            var deletionCount = 0;

            Chunker.ExecuteInChunks(items.Count, (s, e) =>
            {
                logger.InfoFormat("Batching deletion of {0}-{1} audit documents.", s, e);
                var results = database.Batch(items.GetRange(s, e - s + 1), CancellationToken.None);
                logger.InfoFormat("Batching deletion of {0}-{1} audit documents completed.", s, e);

                deletionCount += results.Count(x => x.Deleted == true);
            });

            Chunker.ExecuteInChunks(attachments.Count, (s, e) =>
            {
                database.TransactionalStorage.Batch(accessor =>
                {
                    logger.InfoFormat("Batching deletion of {0}-{1} attachment audit documents.", s, e);
                    for (var idx = s; idx <= e; idx++)
                    {
                        //We want to continue using attachments for now
#pragma warning disable 618
                        accessor.Attachments.DeleteAttachment(attachments[idx], null);
#pragma warning restore 618
                    }

                    logger.InfoFormat("Batching deletion of {0}-{1} attachment audit documents completed.", s, e);
                });
            });

            if (deletionCount == 0)
            {
                logger.Info("No expired audit documents found");
            }
            else
            {
                logger.InfoFormat("Deleted {0} expired audit documents. Batch execution took {1}ms", deletionCount, stopwatch.ElapsedMilliseconds);
            }
        }

        static bool TryGetBodyId(RavenJObject doc, out string bodyId)
        {
            bodyId = null;

            bodyId = doc.Value<string>("BodyUrl");

            return bodyId != null;
        }

        static ILog logger = LogManager.GetLogger(typeof(AuditMessageCleaner));
    }
}