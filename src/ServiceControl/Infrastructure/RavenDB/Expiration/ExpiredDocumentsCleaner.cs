namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Globalization;
    using System.Threading;
    using CompositeViews.Messages;
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
        private readonly ILog logger = LogManager.GetLogger("DocumentsExpiryLogger");
        private Timer timer;
        DocumentDatabase Database { get; set; }
        string indexName;
        int deleteFrequencyInSeconds;

        const int DeletionBatchSize = 1024;

        public void Execute(DocumentDatabase database)
        {
            Database = database;
            indexName = new MessagesViewIndex().IndexName;

            deleteFrequencyInSeconds = Settings.ExpirationProcessTimerInSeconds;
            if (deleteFrequencyInSeconds == 0)
            {
                return;
            }

            logger.Info("Initialized expired document cleaner, will check for expired documents every {0} seconds",
                        deleteFrequencyInSeconds);
            timer = new Timer(TimerCallback, null, TimeSpan.FromSeconds(deleteFrequencyInSeconds), Timeout.InfiniteTimeSpan);
        }

        void TimerCallback(object state)
        {
            var currentTime = SystemTime.UtcNow;
            var currentExpiryThresholdTime = currentTime.AddHours(-Settings.HoursToKeepMessagesBeforeExpiring);
            logger.Debug("Trying to find expired documents to delete (with threshold {0})", currentExpiryThresholdTime.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
            const string queryString = "Status:3 OR Status:4";
            var query = new IndexQuery
            {
                Start = 0,
                //Cutoff = currentTime,
                Query = queryString,
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

            try
            {
                // we may be receiving a LOT of documents to delete, so we are going to skip
                // the cache for that, to avoid filling it up very quickly
                using (DocumentCacher.SkipSettingDocumentsInDocumentCache())
                using (Database.DisableAllTriggersForCurrentThread())
                using (var cts = new CancellationTokenSource())
                {
                    var documentWithCurrentThresholdTimeReached = false;
                    var items = new List<ICommandData>(DeletionBatchSize);
                    
                    Database.Query(indexName, query, CancellationTokenSource.CreateLinkedTokenSource(Database.WorkContext.CancellationToken, cts.Token).Token,
                        information => logger.Debug("Found {0} docs to expire, starting deleting in bulks", information.TotalResults),
                        doc =>
                        {
                            if (documentWithCurrentThresholdTimeReached)
                                return;

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

                                if (items.Count%DeletionBatchSize == 0)
                                {
                                    docsToExpire += items.Count;
                                    Database.Batch(items.ToArray());
                                    items.Clear();
                                }
                            }
                        });

                    if (items.Count > 0)
                    {
                        docsToExpire += items.Count;
                        Database.Batch(items.ToArray());
                        items.Clear();
                    }
                }

                if (docsToExpire == 0)
                {
                    logger.Debug("No expired documents found");
                }
                else
                {
                    logger.Debug(() => string.Format("Deleted {0} expired documents", docsToExpire));
                }
            }
            catch (Exception e)
            {
                logger.ErrorException("Error when trying to find expired documents", e);
            }
            finally
            {
                timer.Change(TimeSpan.FromSeconds(deleteFrequencyInSeconds), Timeout.InfiniteTimeSpan);
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
                timer.Dispose();
            }
        }
    }
}
