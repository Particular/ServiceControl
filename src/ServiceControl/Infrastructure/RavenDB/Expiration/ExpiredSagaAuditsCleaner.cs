namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using Raven.Database.Impl;
    using Raven.Json.Linq;

    public class ExpiredSagaAuditsCleaner
    {
        static ILog logger = LogManager.GetLogger(typeof(ExpiredSagaAuditsCleaner));

        public static void ExpireProcessedMessages(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold)
        {
            using (DocumentCacher.SkipSettingDocumentsInDocumentCache())
            using (database.DisableAllTriggersForCurrentThread())
            using (var cts = new CancellationTokenSource())
            {
                var stopwatch = Stopwatch.StartNew();
                var documentWithCurrentThresholdTimeReached = false;
                var items = new List<ICommandData>(deletionBatchSize);
                var attachments = new List<string>(deletionBatchSize);
                var docsToExpire = 0;
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
                        },
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

                logger.Debug("Batching deletion of {0} documents.", items.Count);

                docsToExpire += items.Count;
                var results = database.Batch(items.ToArray());
                database.TransactionalStorage.Batch(accessor =>
                {
                    foreach (var attach in attachments)
                    {
                        accessor.Attachments.DeleteAttachment(attach, null);
                    }
                });
                var deletionCount = results.Count(x => x.Deleted == true);
                if (docsToExpire == 0)
                {
                    logger.Debug("No expired documents found");
                }
                else
                {
                    logger.Debug("Deleted {0} out of {1} expired documents batch - Execution time:{2}ms", deletionCount, docsToExpire, stopwatch.ElapsedMilliseconds);
                }
            }
        }


        public static void ExpireSagaAudits(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold)
        {
            using (DocumentCacher.SkipSettingDocumentsInDocumentCache())
            using (database.DisableAllTriggersForCurrentThread())
            using (var cts = new CancellationTokenSource())
            {
                var stopwatch = Stopwatch.StartNew();
                var documentWithCurrentThresholdTimeReached = false;
                var items = new List<ICommandData>(deletionBatchSize);
                var docsToExpire = 0;
                try
                {
                    var query = new IndexQuery
                    {
                        Start = 0,
                        PageSize = deletionBatchSize,
                        FieldsToFetch = new[]
                        {
                            "__document_id",
                            "LastModified",
                        },
                        SortedFields = new[]
                        {
                            new SortedField("LastModified")
                            {
                                Descending = false
                            }
                        },
                    };
                    var indexName = new ExpirySagaAuditIndex().IndexName;
                    database.Query(indexName, query, CancellationTokenSource.CreateLinkedTokenSource(database.WorkContext.CancellationToken, cts.Token).Token,
                        null,
                        doc =>
                        {
                            if (documentWithCurrentThresholdTimeReached)
                            {
                                return;
                            }

                            if (doc.Value<DateTime>("LastModified") >= expiryThreshold)
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
                        });
                }
                catch (OperationCanceledException)
                {
                    //Ignore
                }

                logger.Debug("Batching deletion of {0} documents.", items.Count);

                docsToExpire += items.Count;
                var results = database.Batch(items.ToArray());
                var deletionCount = results.Count(x => x.Deleted == true);
                if (docsToExpire == 0)
                {
                    logger.Debug("No expired documents found");
                }
                else
                {
                    logger.Debug("Deleted {0} out of {1} expired documents batch - Execution time:{2}ms", deletionCount, docsToExpire, stopwatch.ElapsedMilliseconds);
                }
            }
        }

        static bool TryGetBodyId(RavenJObject doc, out string bodyId)
        {
            bodyId = null;
            var bodyNotStored = doc.SelectToken("MessageMetadata.BodyNotStored", errorWhenNoMatch: false);
            if (bodyNotStored != null && bodyNotStored.Value<bool>())
            {
                return false;
            }
            var messageId = doc.SelectToken("MessageMetadata.MessageId", errorWhenNoMatch: false);
            if (messageId == null)
            {
                return false;
            }
            bodyId = "messagebodies/" + messageId.Value<string>();
            return true;
        }
    }
}