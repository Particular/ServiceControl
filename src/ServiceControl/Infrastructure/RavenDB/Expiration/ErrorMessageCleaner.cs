namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Database;
    using Raven.Database.Impl;
    using Raven.Json.Linq;

    public static class ErrorMessageCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(ErrorMessageCleaner));

        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold)
        {
            using (DocumentCacher.SkipSettingDocumentsInDocumentCache())
            using (database.DisableAllTriggersForCurrentThread())
            {
                var stopwatch = Stopwatch.StartNew();
                var items = new List<string>(deletionBatchSize);
                var attachments = new List<string>(deletionBatchSize);
                try
                {
                    var query = new IndexQuery
                    {
                        Start = 0,
                        PageSize = deletionBatchSize,
                        Cutoff = SystemTime.UtcNow,
                        Query = string.Format("Status:[2 TO 4] AND LastModified:[* TO {0}]", expiryThreshold.Ticks),
                        FieldsToFetch = new[]
                        {
                            "__document_id",
                            "MessageId"
                        },
                        SortedFields = new[]
                        {
                            new SortedField("LastModified")
                            {
                                Field = "LastModified",
                                Descending = false
                            }
                        },
                    };
                    var indexName = new ExpiryErrorMessageIndex().IndexName;
                    database.Query(indexName, query, database.WorkContext.CancellationToken,
                        null,
                        doc =>
                        {
                            var id = doc.Value<string>("__document_id");
                            if (string.IsNullOrEmpty(id))
                            {
                                return;
                            }

                            items.Add(id);

                            attachments.Add(doc.Value<string>("MessageId"));
                        });
                }
                catch (OperationCanceledException)
                {
                    //Ignore
                }

                var deletionCount = 0;

                database.TransactionalStorage.Batch(accessor =>
                {
                    RavenJObject metadata;
                    Etag deletedETag;
                    logger.InfoFormat("Batching deletion of {0} error documents.", items.Count);
                    deletionCount += items.Count(key => accessor.Documents.DeleteDocument(key, null, out metadata, out deletedETag));
                    logger.InfoFormat("Batching deletion of {0} error documents completed.", items.Count);

                    logger.InfoFormat("Batching deletion of {0} attachment error documents.", attachments.Count);
                    foreach (var attach in attachments)
                    {
                        accessor.Attachments.DeleteAttachment("messagebodies/" + attach, null);
                    }
                    logger.InfoFormat("Batching deletion of {0} attachment error documents completed.", attachments.Count);
                });

                if (deletionCount == 0)
                {
                    logger.Info("No expired error documents found");
                }
                else
                {
                    logger.InfoFormat("Deleted {0} expired error documents. Batch execution took {1}ms", deletionCount, stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }
}