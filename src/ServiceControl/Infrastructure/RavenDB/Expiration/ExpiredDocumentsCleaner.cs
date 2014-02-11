using System;
using System.Collections.Generic;

namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.ComponentModel.Composition;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using CompositeViews.Messages;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using Raven.Database.Data;
    using Raven.Database.Plugins;
    using ServiceBus.Management.Infrastructure.Settings;

    [InheritedExport(typeof(IStartupTask))]
    [ExportMetadata("Bundle", "customDocumentExpiration")]
    public class ExpiredDocumentsCleaner : IStartupTask, IDisposable
    {
        private readonly ILog logger = LogManager.GetCurrentClassLogger();
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
                return;

            logger.Info("Initialized expired document cleaner, will check for expired documents every {0} seconds",
                        deleteFrequencyInSeconds);
            timer = new Timer(TimerCallback, null, TimeSpan.FromSeconds(deleteFrequencyInSeconds), TimeSpan.FromSeconds(deleteFrequencyInSeconds));
        }

        private void TimerCallback(object state)
        {
            if (executing)
                return;

            executing = true;
            try
            {
                DateTime currentTime = SystemTime.UtcNow;
                var currentExpiryThresholdTime = currentTime.AddHours(-Settings.HoursToKeepMessagesBeforeExpiring);
                var expiryThresholdAsStr = currentExpiryThresholdTime.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture);
                logger.Debug("Trying to find expired documents to delete");                
                var query = "Status:3 AND ProcessedAt:[* TO " + expiryThresholdAsStr + "]"; // MessageStatus.Successful more than Settings.HoursToKeepMessagesBeforeExpiring hours old

                var list = new List<string>();
                int start = 0;
                while (true)
                {
                    const int pageSize = 1024;

                    QueryResultWithIncludes queryResult;
                    //using (var cts = new CancellationTokenSource())
                    using (Database.DisableAllTriggersForCurrentThread())
                    {
                        //cts.TimeoutAfter(TimeSpan.FromMinutes(5));
                        queryResult = Database.Query(indexName, new IndexQuery
                        {
                            Start = start,
                            PageSize = pageSize,
                            Cutoff = currentTime,
                            Query = query,
                            FieldsToFetch = new[] { "__document_id" }
                        }/* , cts.Token TODO latest RavenDB requires this */);
                    }

                    if (queryResult.Results.Count == 0)
                        break;

                    start += pageSize;

                    list.AddRange(queryResult.Results.Select(result => result.Value<string>("__document_id")).Where(x => string.IsNullOrEmpty(x) == false));
                }

                if (list.Count == 0)
                    return;

                logger.Debug(
                    () => string.Format("Deleting {0} expired documents: [{1}]", list.Count, string.Join(", ", list)));

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
                timer.Dispose();
        }
    }
}
