namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.ComponentModel.Composition;
    using System.Threading.Tasks;
    using BackgroundTasks;
    using NServiceBus.Logging;
    using Raven.Database;
    using Raven.Database.Plugins;

    [InheritedExport(typeof(IStartupTask))]
    [ExportMetadata("Bundle", "customDocumentExpiration")]
    public class ExpiredDocumentsCleanerBundle : IStartupTask, IDisposable
    {
        public void Dispose()
        {
            lock (this)
            {
                if (timer == null)
                {
                    return;
                }

                var stopTask = timer.Stop();
                var delayTask = Task.Delay(TimeSpan.FromSeconds(30));
                var composite = Task.WhenAny(stopTask, delayTask);

                var finishedTask = composite.GetAwaiter().GetResult();
                if (finishedTask == delayTask)
                {
                    logger.Error("Cleanup process did not finish on time. Forcing shutdown.");
                }
                else
                {
                    logger.Info("Expired documents cleanup process stopped.");
                }

                timer = null;
            }
        }

        public void Execute(DocumentDatabase database)
        {
            var deleteFrequencyInSeconds = RavenBootstrapper.Settings.ExpirationProcessTimerInSeconds;

            if (deleteFrequencyInSeconds == 0)
            {
                return;
            }

            var due = TimeSpan.FromSeconds(deleteFrequencyInSeconds);
            var deletionBatchSize = RavenBootstrapper.Settings.ExpirationProcessBatchSize;

            logger.Info($"Running deletion of expired documents every {deleteFrequencyInSeconds} seconds");
            logger.Info($"Deletion batch size set to {deletionBatchSize}");
            logger.Info($"Retention period for errors is {RavenBootstrapper.Settings.ErrorRetentionPeriod}");

            var auditRetention = RavenBootstrapper.Settings.AuditRetentionPeriod;

            if (auditRetention.HasValue)
            {
                logger.InfoFormat("Retention period for audits and saga history is {0}", RavenBootstrapper.Settings.AuditRetentionPeriod);
            }

            timer = new TimerJob(
                token => ExpiredDocumentsCleaner.RunCleanup(deletionBatchSize, database, RavenBootstrapper.Settings, token), due, due, e => { logger.Error("Error when trying to find expired documents", e); });
        }

        ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleanerBundle));
        TimerJob timer;
    }
}