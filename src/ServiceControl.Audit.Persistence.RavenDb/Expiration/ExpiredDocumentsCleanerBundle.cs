namespace ServiceControl.Audit.Persistence.RavenDB.Expiration
{
    using System;
    using System.ComponentModel.Composition;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Database;
    using Raven.Database.Plugins;
    using ServiceControl.Audit.Infrastructure;

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
            var deleteFrequencyInSeconds = int.Parse(RavenBootstrapper.Settings.PersisterSpecificSettings["RavenDb/ExpirationProcessTimerInSeconds"]);

            if (deleteFrequencyInSeconds == 0)
            {
                return;
            }

            var due = TimeSpan.FromSeconds(deleteFrequencyInSeconds);
            var deletionBatchSize = int.Parse(RavenBootstrapper.Settings.PersisterSpecificSettings["RavenDb/ExpirationProcessBatchSize"]);
            var auditRetentionPeriod = TimeSpan.Parse(RavenBootstrapper.Settings.PersisterSpecificSettings["RavenDb/ExpirationProcessTimerInSeconds"]);
            logger.Info($"Running deletion of expired documents every {deleteFrequencyInSeconds} seconds");
            logger.Info($"Deletion batch size set to {deletionBatchSize}");
            logger.Info($"Retention period for audits and saga history is {auditRetentionPeriod}");

            timer = new AsyncTimer(
                token => ExpiredDocumentsCleaner.RunCleanup(deletionBatchSize, database, auditRetentionPeriod, token), due, due, e => { logger.Error("Error when trying to find expired documents", e); });
        }

        ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleanerBundle));
        AsyncTimer timer;
    }
}