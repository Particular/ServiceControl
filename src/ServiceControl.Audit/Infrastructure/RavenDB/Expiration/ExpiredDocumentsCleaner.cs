namespace ServiceControl.Audit.Infrastructure.RavenDB.Expiration
{
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Abstractions;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using Settings;

    class ExpiredDocumentsCleaner
    {
        public static Task<TimerJobExecutionResult> RunCleanup(int deletionBatchSize, DocumentDatabase database, Settings settings, CancellationToken token)
        {
            var threshold = SystemTime.UtcNow.Add(-settings.AuditRetentionPeriod);

            logger.Debug("Trying to find expired ProcessedMessage and SagaHistory documents to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
            AuditMessageCleaner.Clean(deletionBatchSize, database, threshold, token);
            logger.Debug("Trying to find expired KnownEndpoints to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
            KnownEndpointsCleaner.Clean(deletionBatchSize, database, threshold, token);

            return Task.FromResult(TimerJobExecutionResult.ScheduleNextExecution);
        }

        static ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleaner));
    }
}