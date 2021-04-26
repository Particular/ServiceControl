namespace ServiceControl.Audit.Infrastructure.RavenDB.Expiration
{
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Abstractions;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using ServiceControl.SagaAudit;
    using Settings;

    class ExpiredDocumentsCleaner
    {
        public static Task<TimerJobExecutionResult> RunCleanup(int deletionBatchSize, DocumentDatabase database, Settings settings, CancellationToken cancellationToken = default)
        {
            var threshold = SystemTime.UtcNow.Add(-settings.AuditRetentionPeriod);

            logger.Debug("Trying to find expired ProcessedMessage, SagaHistory and KnownEndpoint documents to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
            AuditMessageCleaner.Clean(deletionBatchSize, database, threshold, cancellationToken);
            KnownEndpointsCleaner.Clean(deletionBatchSize, database, threshold, cancellationToken);
            SagaHistoryCleaner.Clean(deletionBatchSize, database, threshold, cancellationToken);

            return Task.FromResult(TimerJobExecutionResult.ScheduleNextExecution);
        }

        static ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleaner));
    }
}