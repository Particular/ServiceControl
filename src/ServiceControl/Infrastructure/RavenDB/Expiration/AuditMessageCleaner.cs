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
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceControl.Operations.BodyStorage;

    public static class AuditMessageCleaner
    {
        const int CHUNK_SIZE = 128;

        private static readonly ILog logger = LogManager.GetLogger(typeof(AuditMessageCleaner));

        public static void Clean(int deletionBatchSize, IDocumentStore store, DateTime expiryThreshold, CancellationToken token, IMessageBodyStore messageBodyStore)
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
            var items = new List<ICommandData>(CHUNK_SIZE);
            var attachments = new List<string>(CHUNK_SIZE);

            logger.Info("Starting clean-up of expired audit documents.");

            QueryHeaderInformation _;
            var deletionCount = 0;
            var stopwatch = Stopwatch.StartNew();
            using (var ie = store.DatabaseCommands.StreamQuery(indexName, query, out _))
            {
                while (ie.MoveNext())
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    
                    var doc = ie.Current;
                    var id = doc.Value<string>("__document_id");
                    if (string.IsNullOrEmpty(id))
                    {
                        return;
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

                    if (items.Count < CHUNK_SIZE)
                    {
                        continue;
                    }

                    deletionCount += Delete(store, token, messageBodyStore, items, attachments);
                    
                    attachments.Clear();
                    items.Clear();
                }
            }

            deletionCount += Delete(store, token, messageBodyStore, items, attachments);

            if (deletionCount == 0)
            {
                logger.Info("No expired audit documents found");
            }
            else
            {
                logger.Info($"Deleted {deletionCount} expired audit documents in {stopwatch.ElapsedMilliseconds}ms.");
            }
        }

        static int Delete(IDocumentStore store, CancellationToken token, IMessageBodyStore messageBodyStore, List<ICommandData> items, List<string> attachments)
        {
            var stopwatch = Stopwatch.StartNew();
            if (attachments.Count > 0)
            {
                foreach (var att in attachments)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        messageBodyStore.Delete(att);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Deletion of attachment failed.", ex);
                    }
                }

                logger.Info($"Deleted {attachments.Count} attachments for expired audit documents in {stopwatch.ElapsedMilliseconds}ms.");
            }


            if (token.IsCancellationRequested)
            {
                return 0;
            }

            var deletionCount = 0;

            if (items.Count > 0)
            {
                stopwatch.Restart();

                var results = store.DatabaseCommands.Batch(items);
                deletionCount = results.Count(x => x.Deleted == true);
                logger.Info($"Deleted {deletionCount} expired audit documents in {stopwatch.ElapsedMilliseconds}ms.");
            }

            return deletionCount;
        }

        private static bool TryGetBodyId(RavenJObject doc, out string bodyId)
        {
            bodyId = null;
            var bodyNotStored = doc.SelectToken("MessageMetadata.BodyNotStored", false);
            if ((bodyNotStored != null) && bodyNotStored.Value<bool>())
            {
                return false;
            }
            var messageId = doc.SelectToken("MessageMetadata.MessageId", false);
            if (messageId == null)
            {
                return false;
            }
            bodyId = messageId.Value<string>();
            return true;
        }
    }
}