namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public static class ErrorMessageCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(ErrorMessageCleaner));

        public static void Clean(IDocumentStore store, DateTime expiryThreshold)
        {
            var query = new IndexQuery
            {
                Cutoff = SystemTime.UtcNow,
                DisableCaching = true,
                Query = $"Status:[2 TO 4] AND LastModified:[* TO {expiryThreshold.Ticks}]",
            };

            var indexName = new ExpiryErrorMessageIndex().IndexName;

            logger.Info("Starting clean-up of expired error documents.");
            store.DatabaseCommands.DeleteByIndex(indexName, query, new BulkOperationOptions
            {
                AllowStale = true,
                RetrieveDetails = false
            });
        }
    }
}
