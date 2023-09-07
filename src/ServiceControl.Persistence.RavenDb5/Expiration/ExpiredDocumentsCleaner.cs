namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using BackgroundTasks;
    using NServiceBus.Logging;
    using Raven.Client.Util;

    class ExpiredDocumentsCleaner
    {
        public static Task<TimerJobExecutionResult> RunCleanup(int deletionBatchSize, DocumentDatabase database, RavenDBPersisterSettings settings, CancellationToken cancellationToken = default)
        {
            var threshold = SystemTime.UtcNow.Add(settings.ErrorRetentionPeriod);

            if (logger.IsDebugEnabled)
            {
                logger.Debug($"Trying to find expired FailedMessage documents to delete (with threshold {threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture)})");
            }
            ErrorMessageCleaner.Clean(deletionBatchSize, database, threshold, cancellationToken);

            threshold = SystemTime.UtcNow.Add(settings.EventsRetentionPeriod);

            if (logger.IsDebugEnabled)
            {
                logger.Debug($"Trying to find expired EventLogItem documents to delete (with threshold {threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture)})");
            }
            EventLogItemsCleaner.Clean(deletionBatchSize, database, threshold, cancellationToken);

            return Task.FromResult(TimerJobExecutionResult.ScheduleNextExecution);
        }

        static ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleaner));
    }
}