namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using NServiceBus.Logging;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Json.Linq;

    public static class AuditMessageCleaner
    {
        static ILog logger = LogManager.GetLogger(typeof(AuditMessageCleaner));

        public static void Clean(int deletionBatchSize, IDocumentStore store, DateTime expiryThreshold, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();

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
                    "MessageMetadata"
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
            QueryHeaderInformation _;
            var deletionCount = 0;

            logger.Info("Batching deletion of audit documents.");

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

                    string bodyId;
                    if (TryGetBodyId(doc, out bodyId))
                    {
                        try
                        {
                            store.DatabaseCommands.DeleteAttachment(bodyId, null);
                        }
                        catch (Exception ex)
                        {
                            logger.Warn("Deletion of attachment failed.", ex);
                        }
                    }

                    if (deletionCount >= deletionBatchSize)
                    {
                        break;
                    }
                }
            }

            logger.Info("Batching deletion of audit documents completed.");

            if (deletionCount == 0)
            {
                logger.Info("No expired audit documents found.");
            }
            else
            {
                logger.InfoFormat("Deleted {0} expired audit documents. Batch execution took {1}ms.", deletionCount, stopwatch.ElapsedMilliseconds);
            }
        }

        static bool TryGetBodyId(RavenJObject doc, out string bodyId)
        {
            bodyId = null;
            var bodyNotStored = doc.SelectToken("MessageMetadata.BodyNotStored", false);
            if (bodyNotStored != null && bodyNotStored.Value<bool>())
            {
                return false;
            }
            var messageId = doc.SelectToken("MessageMetadata.MessageId", false);
            if (messageId == null)
            {
                return false;
            }
            bodyId = "messagebodies/" + messageId.Value<string>();
            return true;
        }
    }
}