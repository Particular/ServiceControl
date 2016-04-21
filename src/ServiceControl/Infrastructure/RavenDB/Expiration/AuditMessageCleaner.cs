namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using NServiceBus.Logging;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Json.Linq;
    using System.Linq;
    using System.Threading.Tasks;

    public static class AuditMessageCleaner
    {
        static ILog logger = LogManager.GetLogger(typeof(AuditMessageCleaner));

        public static void Clean(int deletionBatchSize, IDocumentStore store, DateTime expiryThreshold)
        {
            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            var attachments = new List<string>(deletionBatchSize);

            var query = new IndexQuery
            {
                Start = 0,
                PageSize = deletionBatchSize,
                Cutoff = SystemTime.UtcNow,
                DisableCaching = true,
                Query = string.Format("ProcessedAt:[* TO {0}]", expiryThreshold.Ticks),
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

                    string bodyId;
                    if (TryGetBodyId(doc, out bodyId))
                    {
                        attachments.Add(bodyId);
                    }
                }
            }

            var deletionCount = 0;

            Chunker.ExecuteInChunks(items.Count, (s, e) =>
            {
                logger.InfoFormat("Batching deletion of {0}-{1} audit documents.", s, e);
                var results = store.DatabaseCommands.Batch(items.GetRange(s, e - s + 1));
                logger.InfoFormat("Batching deletion of {0}-{1} audit documents completed.", s, e);

                deletionCount += results.Count(x => x.Deleted == true);
            });

            logger.InfoFormat("Deletion of {0}-{1} attachment audit documents.", 0, attachments.Count);
            try
            {
                Parallel.ForEach(attachments, attach =>
                {
                    store.DatabaseCommands.DeleteAttachment(attach, null);
                });
            }
            catch (AggregateException ex)
            {
                logger.Warn("Deletion of attachments failed", ex);
            }
            logger.InfoFormat("Deletion of {0}-{1} attachment audit documents completed.", 0, attachments.Count);

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