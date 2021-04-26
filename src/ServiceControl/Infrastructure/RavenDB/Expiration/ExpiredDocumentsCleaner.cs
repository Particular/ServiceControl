namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Abstractions;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using SagaAudit;
    using ServiceBus.Management.Infrastructure.Settings;

    class ExpiredDocumentsCleaner
    {
        public static Task<TimerJobExecutionResult> RunCleanup(int deletionBatchSize, DocumentDatabase database, Settings settings, CancellationToken cancellationToken = default)
        {
            var threshold = SystemTime.UtcNow.Add(-settings.ErrorRetentionPeriod);

            logger.Debug("Trying to find expired FailedMessage documents to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
            ErrorMessageCleaner.Clean(deletionBatchSize, database, threshold, cancellationToken);

            threshold = SystemTime.UtcNow.Add(-settings.EventsRetentionPeriod);

            logger.Debug("Trying to find expired EventLogItem documents to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
            EventLogItemsCleaner.Clean(deletionBatchSize, database, threshold, cancellationToken);

            if (settings.AuditRetentionPeriod.HasValue)
            {
                threshold = SystemTime.UtcNow.Add(-settings.AuditRetentionPeriod.Value);

                logger.Debug("Trying to find expired ProcessedMessage and SagaHistory documents to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
                AuditMessageCleaner.Clean(deletionBatchSize, database, threshold, cancellationToken);
                SagaHistoryCleaner.Clean(deletionBatchSize, database, threshold, cancellationToken);
            }

            return Task.FromResult(TimerJobExecutionResult.ScheduleNextExecution);
        }

        static ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleaner));
    }
}