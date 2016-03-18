namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Database;
    using Raven.Database.Impl;

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

                logger.DebugFormat("Batching deletion of {0} sagahistory documents.", items.Count);

                docsToExpire += items.Count;
                var results = database.Batch(items.ToArray());
                var deletionCount = results.Count(x => x.Deleted == true);
                if (docsToExpire == 0)
                {
                    logger.Debug("No expired sagahistory documents found");
                }
                else
                {
                    logger.InfoFormat("Deleted {0} out of {1} expired sagahistory documents. Batch execution took {2}ms", deletionCount, docsToExpire, stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }
}