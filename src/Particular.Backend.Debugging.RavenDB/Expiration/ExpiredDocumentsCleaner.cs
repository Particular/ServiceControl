namespace Particular.Backend.Debugging.RavenDB.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using Raven.Database.Impl;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ExpiredDocumentsCleaner
    {
        ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleanerTask));
        string indexName = new ExpiryProcessedMessageIndex().IndexName;

        public void TryClean(DateTime currentTime, DocumentDatabase database, int deletionBatchSize)
        {
            var currentExpiryThresholdTime = currentTime.AddHours(-Settings.HoursToKeepMessagesBeforeExpiring);
            logger.Debug("Trying to find expired documents to delete (with threshold {0})", currentExpiryThresholdTime.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
            var query = new IndexQuery
            {
                Start = 0,
                PageSize = deletionBatchSize,
                Cutoff = currentTime,
                FieldsToFetch = new[]
                {
                    "__document_id",
                    "ProcessedAt"
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

            var docsToExpire = 0;
            // we may be receiving a LOT of documents to delete, so we are going to skip
            // the cache for that, to avoid filling it up very quickly
            var stopwatch = Stopwatch.StartNew();
            int deletionCount;
            using (DocumentCacher.SkipSettingDocumentsInDocumentCache())
            using (database.DisableAllTriggersForCurrentThread())
            using (var cts = new CancellationTokenSource())
            {
                var documentWithCurrentThresholdTimeReached = false;
                var items = new List<ICommandData>(deletionBatchSize);
                database.Query(indexName, query, CancellationTokenSource.CreateLinkedTokenSource(database.WorkContext.CancellationToken, cts.Token).Token,
                    null,
                    doc =>
                    {
                        if (documentWithCurrentThresholdTimeReached)
                        {
                            return;
                        }

                        if (doc.Value<DateTime>("ProcessedAt") >= currentExpiryThresholdTime)
                        {
                            documentWithCurrentThresholdTimeReached = true;
                            cts.Cancel();
                            return;
                        }

                        var id = doc.Value<string>("__document_id");
                        if (!string.IsNullOrEmpty(id))
                        {
                            items.Add(new DeleteCommandData
                            {
                                Key = id
                            });
                        }
                    });
                docsToExpire += items.Count;
                var results = database.Batch(items.ToArray());
                deletionCount = results.Count(x => x.Deleted == true);
                items.Clear();
            }

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
}