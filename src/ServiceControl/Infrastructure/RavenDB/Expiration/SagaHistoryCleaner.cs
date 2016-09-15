namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public static class SagaHistoryCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(SagaHistoryCleaner));

        public static void Clean(IDocumentStore store, DateTime expiryThreshold)
        {
            var query = new IndexQuery
            {
                DisableCaching = true,
                Cutoff = SystemTime.UtcNow,
                Query = $"LastModified:[* TO {expiryThreshold.Ticks}]",
            };

            var indexName = new ExpirySagaAuditIndex().IndexName;

            logger.Info("Starting clean-up of expired sagahistory documents.");
            store.DatabaseCommands.DeleteByIndex(indexName, query, new BulkOperationOptions
            {
                AllowStale = true,
                RetrieveDetails = false
            });
        }
    }
}