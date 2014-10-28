namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Globalization;
    using System.Threading;
    using CompositeViews.Messages;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using Raven.Database.Data;
    using Raven.Database.Extensions;
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

        private volatile bool executing;

        public void Execute(DocumentDatabase database)
        {
            Database = database;
            indexName = new MessagesViewIndex().IndexName;

            var deleteFrequencyInSeconds = Settings.ExpirationProcessTimerInSeconds;
            if (deleteFrequencyInSeconds == 0)
            {
                return;
            }

            logger.Info("Initialized expired document cleaner, will check for expired documents every {0} seconds",
                        deleteFrequencyInSeconds);
            timer = new Timer(TimerCallback, null, TimeSpan.FromSeconds(deleteFrequencyInSeconds), TimeSpan.FromSeconds(deleteFrequencyInSeconds));
        }

        private void TimerCallback(object state)
        {
            if (executing)
            {
                return;
            }

            executing = true;
            
            try
            {
                var currentTime = SystemTime.UtcNow;
                var currentExpiryThresholdTime = currentTime.AddHours(-Settings.HoursToKeepMessagesBeforeExpiring);
                logger.Debug("Trying to find expired documents to delete (with threshold {0})", currentExpiryThresholdTime.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
                const string query = "(Status:3 OR Status:4)";

                var pageSize = 10; // Prevent periodic checks from being too expensive
                var list = new List<string>();
                var start = 0;
                while (true)
                {
                    QueryResultWithIncludes queryResult;
                    using (var cts = new CancellationTokenSource())
                    using (Database.DisableAllTriggersForCurrentThread())
                    {
                        cts.TimeoutAfter(TimeSpan.FromMinutes(5));
                        queryResult = Database.Query(indexName, new IndexQuery
                        {
                            Start = start,
                            PageSize = pageSize,
                            Cutoff = currentTime,
                            Query = query,
                            FieldsToFetch = new[] { "__document_id", "ProcessedAt" },
                            SortedFields = new[] { new SortedField("ProcessedAt") { Field = "ProcessedAt", Descending = false } },
                        } , cts.Token);
                    }

                    if (queryResult.Results.Count == 0)
                    {
                        break;
                    }

                    var documentWithCurrentThresholdTimeReached = false;
                    foreach (var result in queryResult.Results)
                    {
                        if (result.Value<DateTime>("ProcessedAt") >= currentExpiryThresholdTime)
                        {
                            documentWithCurrentThresholdTimeReached = true;
                            break;
                        }

                        var id = result.Value<string>("__document_id");
                        if (!string.IsNullOrEmpty(id))
                        {
                            list.Add(id);
                        }
                    }

                    if (documentWithCurrentThresholdTimeReached || queryResult.Results.Count < pageSize)
                    {
                        break;
                    }

                    start += pageSize;

                    // If we found results, we bump pageSize to start working in bulks
                    if (pageSize < 1024)
                    {
                        pageSize = 1024;
                    }
                }

                if (list.Count == 0)
                {
                    logger.Debug("No expired documents found");
                    return;
                }

                logger.Debug(() => string.Format("Deleting {0} expired documents", list.Count));

                foreach (var id in list)
                {
                    Database.Delete(id, null, null);
                }
            }
            catch (Exception e)
            {
                logger.ErrorException("Error when trying to find expired documents", e);
            }
            finally
            {
                executing = false;
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
