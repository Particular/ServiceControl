namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.ComponentModel.Composition;
    using System.Threading.Tasks;
    using BackgroundTasks;
    using NServiceBus.Logging;
    using Raven.Database;
    using Raven.Database.Plugins;
    using ServiceControl.Persistence;

    [InheritedExport(typeof(IStartupTask))]
    [ExportMetadata("Bundle", "customDocumentExpiration")]
    public class ExpiredDocumentsCleanerBundle : IStartupTask, IDisposable
    {
        // TODO: Ensure that the timers are started when the persister starts!

        PersistenceSettings persistenceSettings;

        public ExpiredDocumentsCleanerBundle(PersistenceSettings persistenceSettings)
        {
            this.persistenceSettings = persistenceSettings;
        }

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
            var deleteFrequencyInSeconds = persistenceSettings.ExpirationProcessTimerInSeconds();

            if (deleteFrequencyInSeconds == 0)
            {
                return;
            }

            var due = TimeSpan.FromSeconds(deleteFrequencyInSeconds);
            var deletionBatchSize = persistenceSettings.ExpirationProcessBatchSize();

            logger.Info($"Running deletion of expired documents every {deleteFrequencyInSeconds} seconds");
            logger.Info($"Deletion batch size set to {deletionBatchSize}");
            logger.Info($"Retention period for errors is {persistenceSettings.ErrorRetentionPeriod}");

            var auditRetention = persistenceSettings.AuditRetentionPeriod;

            if (auditRetention.HasValue)
            {
                logger.InfoFormat("Retention period for audits and saga history is {0}", persistenceSettings.AuditRetentionPeriod);
            }

            timer = new TimerJob(
                token => ExpiredDocumentsCleaner.RunCleanup(deletionBatchSize, database, persistenceSettings, token), due, due, e => { logger.Error("Error when trying to find expired documents", e); });
        }

        ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleanerBundle));
        TimerJob timer;
    }
}