namespace ServiceControl.Infrastructure.RavenDB.Expiration
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
            logger.Info("Retention period for audits and sagahistory is {0}", Settings.AuditRetentionPeriod);
            logger.Info("Retention period for errors is {0}", Settings.ErrorRetentionPeriod);

            var due = TimeSpan.FromSeconds(deleteFrequencyInSeconds);
            timer = new Timer(executor =>
            {
                ExpiredDocumentsCleaner.RunCleanup(deletionBatchSize, database);

                try
                {
                    timer.Change(due, Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException)
                {
                    //Ignored, we are shuting down
                }
            }, null, due, Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
            if (timer != null)
            {
                timer.Dispose();
            }
        }
    }
}
