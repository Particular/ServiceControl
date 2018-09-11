﻿namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Globalization;
    using Raven.Abstractions;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using ServiceBus.Management.Infrastructure.Settings;

    class ExpiredDocumentsCleaner
    {
        public static void RunCleanup(int deletionBatchSize, DocumentDatabase database, Settings settings)
        {
            try
            {
                var threshold = SystemTime.UtcNow.Add(-settings.AuditRetentionPeriod);

                logger.Debug("Trying to find expired ProcessedMessage and SagaHistory documents to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
                AuditMessageCleaner.Clean(deletionBatchSize, database, threshold);
                SagaHistoryCleaner.Clean(deletionBatchSize, database, threshold);

                threshold = SystemTime.UtcNow.Add(-settings.ErrorRetentionPeriod);

                logger.Debug("Trying to find expired FailedMessage documents to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
                ErrorMessageCleaner.Clean(deletionBatchSize, database, threshold);

                threshold = SystemTime.UtcNow.Add(-settings.EventsRetentionPeriod);

                logger.Debug("Trying to find expired EventLogItem documents to delete (with threshold {0})", threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
                EventLogItemsCleaner.Clean(deletionBatchSize, database, threshold);
            }
            catch (Exception e)
            {
                logger.ErrorException("Error when trying to find expired documents", e);
            }
        }

        static ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleaner));
    }
}