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
    using Raven.Database;
    using Raven.Database.Impl;

    public static class ErrorMessageCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(ErrorMessageCleaner));

        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold)
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
                        FieldsToFetch = new[]
                        {
                            "__document_id",
                            "Last-Modified",
                            "MessageId"
                        },
                        SortedFields = new[]
                        {
                            new SortedField("Last-Modified")
                            {
                                Field = "Last-Modified",
                                Descending = false
                            }
                        },
                    };
                    var indexName = new ExpiryErrorMessageIndex().IndexName;
                    database.Query(indexName, query, CancellationTokenSource.CreateLinkedTokenSource(database.WorkContext.CancellationToken, cts.Token).Token,
                        null,
                        doc =>
                        {
                            if (documentWithCurrentThresholdTimeReached)
                            {
                                return;
                            }

                            var id = doc.Value<string>("__document_id");
                            if (string.IsNullOrEmpty(id))
                            {
                                return;
                            }

                            if (doc.Value<DateTime>("Last-Modified") >= expiryThreshold)
                            {
                                documentWithCurrentThresholdTimeReached = true;
                                cts.Cancel();
                                return;
                            }

                            items.Add(new DeleteCommandData
                            {
                                Key = id
                            });

                            attachments.Add(doc.Value<string>("MessageId"));
                        });
                }
                catch (OperationCanceledException)
                {
                    //Ignore
                }

                logger.DebugFormat("Batching deletion of {0} error documents.", items.Count);

                docsToExpire += items.Count;
                var results = database.Batch(items.ToArray());
                database.TransactionalStorage.Batch(accessor =>
                {
                    foreach (var attach in attachments)
                    {
                        accessor.Attachments.DeleteAttachment("messagebodies/" + attach, null);
                    }
                });
                var deletionCount = results.Count(x => x.Deleted == true);
                if (docsToExpire == 0)
                {
                    logger.Debug("No expired error documents found");
                }
                else
                {
                    logger.InfoFormat("Deleted {0} out of {1} expired error documents. Batch execution took {2}ms", deletionCount, docsToExpire, stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }
}