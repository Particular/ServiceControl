namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using NServiceBus.Logging;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public static class AuditMessageCleaner
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(AuditMessageCleaner));

        public static void Clean(IDocumentStore store, DateTime expiryThreshold, bool waitForCompletion = false)
        {
            var query = new IndexQuery
            {
                Cutoff = SystemTime.UtcNow,
                DisableCaching = true,
                Query = $"ProcessedAt:[* TO {expiryThreshold.Ticks}]"
            };


            var indexName = new ExpiryProcessedMessageIndex().IndexName;

            logger.Info("Starting clean-up of expired audit documents.");
            var operation = store.DatabaseCommands.DeleteByIndex(indexName, query, new BulkOperationOptions
            {
                AllowStale = true,
                RetrieveDetails = false,
                MaxOpsPerSec = 1000
            });

            if (waitForCompletion)
            {
                operation.WaitForCompletion();
            }
        }
    }
}
