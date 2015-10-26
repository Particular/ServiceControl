namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Globalization;
    using Raven.Abstractions;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ExpiredDocumentsCleaner
    {
        static ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleaner));

        public static void RunCleanup(int deletionBatchSize, DocumentDatabase database)
        {
            var hoursToKeep = Settings.HoursToKeepMessagesBeforeExpiring;
            var expiryThreshold = SystemTime.UtcNow.AddHours(-hoursToKeep);

            logger.Debug("Trying to find expired documents to delete (with threshold {0})", expiryThreshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));

            try
            {
                ExpiredProcessedMessageCleaner.ExpireProcessedMessages(deletionBatchSize, database, expiryThreshold);
                ExpiredSagaAuditsCleaner. ExpireSagaAudits(deletionBatchSize, database, expiryThreshold);
            }
            catch (Exception e)
            {
                logger.ErrorException("Error when trying to find expired documents", e);
            }
        }
    }
}