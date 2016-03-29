namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Raven.Abstractions.Data;
    using Raven.Database;
    using Raven.Database.Impl;
    using Raven.Json.Linq;

    public static class SagaHistoryCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(SagaHistoryCleaner));

        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold)
        {
            using (DocumentCacher.SkipSettingDocumentsInDocumentCache())
            using (database.DisableAllTriggersForCurrentThread())
            using (var cts = new CancellationTokenSource())
            {
                var stopwatch = Stopwatch.StartNew();
                var documentWithCurrentThresholdTimeReached = false;
                var items = new List<string>(deletionBatchSize);
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
                            items.Add(id);
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
                    logger.InfoFormat("Batching deletion of {0} sagahistory documents.", items.Count);
                    deletionCount += items.Count(key => accessor.Documents.DeleteDocument(key, null, out metadata, out deletedETag));
                    logger.InfoFormat("Batching deletion of {0} sagahistory documents completed.", items.Count);
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
}