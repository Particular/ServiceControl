namespace ServiceControl.Audit.Infrastructure.RavenDB.Expiration
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
    using Raven.Json.Linq;
    using ServiceControl.Infrastructure.RavenDB;

    static class AuditMessageCleaner
    {
        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            var attachments = new List<string>(deletionBatchSize);
            var itemsAndAttachements = Tuple.Create(items, attachments);
            var indexName = new ExpiryProcessedMessageIndex().IndexName;

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
                        "MessageMetadata.MessageId",
                        "MessageMetadata.BodyNotStored"
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
                database.Query(indexName, query, (doc, state) =>
                    {
                        var id = doc.Value<string>("__document_id");
                        if (string.IsNullOrEmpty(id))
                        {
                            return;
                        }

                        state.Item1.Add(new DeleteCommandData
                        {
                            Key = id
                        });

                        if (TryGetBodyId(doc, out var bodyId))
                        {
                            state.Item2.Add(bodyId);
                        }
                    },
                    itemsAndAttachements, cancellationToken);
            }
            catch (IndexDisabledException ex)
            {
                logger.Error($"Unable to cleanup audit messages. The index ${indexName} was disabled.", ex);
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

            var deletedAuditDocuments = Chunker.ExecuteInChunks(items.Count, (itemsForBatch, db, s, e) =>
            {
                logger.Debug($"Batching deletion of {s}-{e} audit documents.");

                var results = db.Batch(itemsForBatch.GetRange(s, e - s + 1), CancellationToken.None);
                logger.Debug($"Batching deletion of {s}-{e} audit documents completed.");

                return results.Count(x => x.Deleted == true);
            }, items, database, cancellationToken);

            var deletedAttachments = Chunker.ExecuteInChunks(attachments.Count, (att, db, s, e) =>
            {
                var deleted = 0;
                logger.Debug($"Batching deletion of {s}-{e} attachment audit documents.");

                db.TransactionalStorage.Batch(accessor =>
                {
                    for (var idx = s; idx <= e; idx++)
                    {
                        //We want to continue using attachments for now
#pragma warning disable 618
                        accessor.Attachments.DeleteAttachment(att[idx], null);
#pragma warning restore 618
                        deleted++;
                    }
                });
                logger.Debug($"Batching deletion of {s}-{e} attachment audit documents completed.");

                return deleted;
            }, attachments, database, cancellationToken);

            if (deletedAttachments + deletedAuditDocuments == 0)
            {
                logger.Debug("No expired audit documents found");
            }
            else
            {
                logger.Debug($"Deleted {deletedAuditDocuments} expired audit documents and {deletedAttachments} message body attachments. Batch execution took {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        static bool TryGetBodyId(RavenJObject doc, out string bodyId)
        {
            bodyId = null;
            if (doc.Value<bool>("MessageMetadata.BodyNotStored"))
            {
                return false;
            }

            var messageId = doc.Value<string>("MessageMetadata.MessageId");
            if (messageId == null)
            {
                return false;
            }

            bodyId = $"messagebodies/{messageId}";
            return true;
        }

        static ILog logger = LogManager.GetLogger(typeof(AuditMessageCleaner));
    }
}