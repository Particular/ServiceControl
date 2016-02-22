﻿namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.ComponentModel.Composition;
    using System.Threading;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using Raven.Database.Plugins;
    using ServiceBus.Management.Infrastructure.Settings;

    [InheritedExport(typeof(IStartupTask))]
    [ExportMetadata("Bundle", "customDocumentExpiration")]
    public class ExpiredDocumentsCleanerBundle : IStartupTask, IDisposable
    {
        ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleanerBundle));
        Timer timer;

        public void Execute(DocumentDatabase database)
        {
            var deleteFrequencyInSeconds = Settings.ExpirationProcessTimerInSeconds;

            if (deleteFrequencyInSeconds == 0)
            {
                return;
            }
            var deletionBatchSize = Settings.ExpirationProcessBatchSize;

            logger.Info("Running deletion of expired documents every {0} seconds", deleteFrequencyInSeconds);
            logger.Info("Deletion batch size set to {0}", deletionBatchSize);
            logger.Info("Retention period is {0} hours", Settings.HoursToKeepMessagesBeforeExpiring);

            var due = TimeSpan.FromSeconds(deleteFrequencyInSeconds);
            timer = new Timer(executor =>
            {
                ExpiredDocumentsCleaner.RunCleanup(deletionBatchSize, database);
                
                timer.Change(due, Timeout.InfiniteTimeSpan);
            }, null, due, Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
            if (timer != null)
            {
                using (var manualResetEvent = new ManualResetEvent(false))
                {
                    timer.Dispose(manualResetEvent);
                    manualResetEvent.WaitOne();
                }
            }
        }
    }
}
