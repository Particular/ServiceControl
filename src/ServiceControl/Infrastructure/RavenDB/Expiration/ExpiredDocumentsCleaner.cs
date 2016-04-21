namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Globalization;
    using System.Threading;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Abstractions;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ExpiredDocumentsCleaner : Feature
    {
        public ExpiredDocumentsCleaner()
        {
            EnableByDefault();
            Prerequisite(c => Settings.ExpirationProcessTimerInSeconds > 0, "Expiration disabled");
            RegisterStartupTask<Cleaner>();
        }

        class Cleaner : FeatureStartupTask
        {
            private readonly TimeKeeper timeKeeper;
            private readonly IDocumentStore store;
            private ILog logger = LogManager.GetLogger(typeof(Cleaner));
            private Timer timer;
            private bool stopping;
            private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            public Cleaner(TimeKeeper timeKeeper, IDocumentStore store)
            {
                this.timeKeeper = timeKeeper;
                this.store = store;
            }

            protected override void OnStart()
            {
                stopping = false;

                var deleteFrequencyInSeconds = Settings.ExpirationProcessTimerInSeconds;
                var deletionBatchSize = Settings.ExpirationProcessBatchSize;

                logger.InfoFormat("Running deletion of expired documents every {0} seconds", deleteFrequencyInSeconds);
                logger.InfoFormat("Deletion batch size set to {0}", deletionBatchSize);
                logger.InfoFormat("Retention period for audits and sagahistory is {0}", Settings.AuditRetentionPeriod);
                logger.InfoFormat("Retention period for errors is {0}", Settings.ErrorRetentionPeriod);

                var due = TimeSpan.FromSeconds(deleteFrequencyInSeconds);
                timer = timeKeeper.New(() =>
                {
                    try
                    {
                        RunCleanup(deletionBatchSize);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Cleaning of expired documents failed.", ex);
                    }
                }, due, due);
            }

            protected override void OnStop()
            {
                cancellationTokenSource.Cancel();

                timeKeeper.Release(timer);
            }

            private void RunCleanup(int deletionBatchSize)
            {
                var threshold = DateTime.UtcNow.Add(-Settings.AuditRetentionPeriod);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                logger.DebugFormat("Trying to find expired ProcessedMessage and SagaHistory documents to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
                try
                {
                    AuditMessageCleaner.Clean(deletionBatchSize, store, threshold, cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    logger.Error("Cleaning of audit expired documents failed.", ex);
                }

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    SagaHistoryCleaner.Clean(deletionBatchSize, store, threshold, cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    logger.Error("Cleaning of sagahistory expired documents failed.", ex);
                }

                threshold = SystemTime.UtcNow.Add(-Settings.ErrorRetentionPeriod);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                logger.DebugFormat("Trying to find expired FailedMessage documents to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
                try
                {
                    ErrorMessageCleaner.Clean(deletionBatchSize, store, threshold, cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    logger.Error("Cleaning of error expired documents failed.", ex);
                }
            }
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
        }
    }
}
