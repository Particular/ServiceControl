﻿namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.ComponentModel.Composition;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using Raven.Database.Plugins;
    using ServiceBus.Management.Infrastructure.Settings;


    [InheritedExport(typeof(IStartupTask))]
    [ExportMetadata("Bundle", "customDocumentExpiration")]
    public class ExpiredDocumentsCleanerBundle : IStartupTask, IDisposable
    {
        static ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleanerBundle));
        PeriodicExecutor timer;

        public void Execute(DocumentDatabase database)
        {
            var deletionBatchSize = Settings.ExpirationProcessBatchSize;
            var deleteFrequencyInSeconds = Settings.ExpirationProcessTimerInSeconds;

            if (deleteFrequencyInSeconds == 0)
            {
                return;
            }

            logger.Info("Expired Documents every {0} seconds", deleteFrequencyInSeconds);
            logger.Info("Deletion Batch Size: {0}", deletionBatchSize);
            logger.Info("Retention Period: {0}", Settings.HoursToKeepMessagesBeforeExpiring);

            timer = new PeriodicExecutor(executor => ExpiredDocumentsCleaner.RunCleanup(Settings.ExpirationProcessBatchSize, database), TimeSpan.FromSeconds(deleteFrequencyInSeconds));
            timer.Start(true);
        }

        public void Dispose()
        {
            if (timer != null)
            {
                timer.Stop();
            }
        }
    }
}
