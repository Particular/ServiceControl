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
    using Raven.Database.Impl;
    using Raven.Json.Linq;

    public static class AuditMessageCleaner
    {
        static ILog logger = LogManager.GetLogger(typeof(AuditMessageCleaner));

        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold)
        {
            using (DocumentCacher.SkipSettingDocumentsInDocumentCache())
            {
                using (database.DisableAllTriggersForCurrentThread())
                {
                    using (var cts = new CancellationTokenSource())
                    {
                        var stopwatch = Stopwatch.StartNew();
                        var documentWithCurrentThresholdTimeReached = false;
                        var items = new List<ICommandData>(deletionBatchSize);
                        var attachments = new List<string>(deletionBatchSize);
                        try
                        {
                            var query = new IndexQuery
                            {
                                Start = 0,
                                PageSize = deletionBatchSize,
                                Cutoff = SystemTime.UtcNow,
                                Query = "Status:3 OR Status:4",
                                FieldsToFetch = new[]
                                {
                                    "__document_id",
                                    "ProcessedAt",
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
                            database.Query(indexName, query, CancellationTokenSource.CreateLinkedTokenSource(database.WorkContext.CancellationToken, cts.Token).Token,
                                null,
                                doc =>
                                {
                                    if (documentWithCurrentThresholdTimeReached)
                                    {
                                        return;
                                    }

                                    if (doc.Value<DateTime>("ProcessedAt") >= expiryThreshold)
                                    {
                                        documentWithCurrentThresholdTimeReached = true;
                                        cts.Cancel();
                                        return;
                                    }

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
                            var results = database.Batch(items.GetRange(s, e - s + 1));
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
                                    accessor.Attachments.DeleteAttachment(attachments[idx], null);
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
                }
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