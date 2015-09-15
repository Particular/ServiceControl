namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
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
    using Raven.Database.Plugins;
    using ServiceBus.Management.Infrastructure.Settings;


    [InheritedExport(typeof(IStartupTask))]
    [ExportMetadata("Bundle", "customDocumentExpiration")]
    public class ExpiredDocumentsCleaner : IStartupTask, IDisposable
    {
        ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleaner));
        PeriodicExecutor timer;
        DocumentDatabase Database { get; set; }
        string indexName;
        int deleteFrequencyInSeconds;
        int deletionBatchSize;

        public void Execute(DocumentDatabase database)
        {
            Database = database;
            indexName = new ExpiryProcessedMessageIndex().IndexName;

            deletionBatchSize = Settings.ExpirationProcessBatchSize;
            deleteFrequencyInSeconds = Settings.ExpirationProcessTimerInSeconds;

            if (deleteFrequencyInSeconds == 0)
            {
                return;
            }

            logger.Info("Expired Documents every {0} seconds", deleteFrequencyInSeconds);
            logger.Info("Deletion Batch Size: {0}", deletionBatchSize);
            logger.Info("Retention Period: {0}", Settings.HoursToKeepMessagesBeforeExpiring);

            timer = new PeriodicExecutor(Delete,TimeSpan.FromSeconds(deleteFrequencyInSeconds));
            timer.Start(true);
        }

        void Delete(PeriodicExecutor executor)
        {
            var currentTime = SystemTime.UtcNow;
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

            try
            {
                var docsToExpire = 0;
                // we may be receiving a LOT of documents to delete, so we are going to skip
                // the cache for that, to avoid filling it up very quickly
                var stopwatch = Stopwatch.StartNew();
                int deletionCount;
                using (DocumentCacher.SkipSettingDocumentsInDocumentCache())
                using (Database.DisableAllTriggersForCurrentThread())
                using (var cts = new CancellationTokenSource())
                {
                    var documentWithCurrentThresholdTimeReached = false;
                    var items = new List<ICommandData>(deletionBatchSize);
                    var attachments = new List<string>(deletionBatchSize);
                    try
                    {
                        Database.Query(indexName, query, CancellationTokenSource.CreateLinkedTokenSource(Database.WorkContext.CancellationToken, cts.Token).Token,
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

                                var bodyNotStored = doc.SelectToken("MessageMetadata.BodyNotStored", errorWhenNoMatch: false);
                                if (bodyNotStored == null || bodyNotStored.Value<bool>() == false)
                                {
                                    var msgId = doc.SelectToken("MessageMetadata.MessageId", errorWhenNoMatch: false);
                                    if (msgId != null)
                                    {
                                        var attachmentId = "messagebodies/" + msgId.Value<string>();
                                        attachments.Add(attachmentId);
                                    }
                                }
                            }
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        //Ignore
                    }

                    logger.Debug("Batching deletion of {0} documents.",items.Count);

                    docsToExpire += items.Count;
                    var results = Database.Batch(items.ToArray());
                    Database.TransactionalStorage.Batch(accessor =>
                    {
                        foreach (var attach in attachments)
                        {
                            accessor.Attachments.DeleteAttachment(attach, null);
                        }
                    });
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
            catch (Exception e)
            {
                logger.ErrorException("Error when trying to find expired documents", e);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (timer != null)
            {
                timer.Stop();
            }
        }
    }
}
