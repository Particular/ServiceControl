namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public static class AuditMessageCleaner
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(AuditMessageCleaner));

        public static void Clean(IDocumentStore store, DateTime expiryThreshold, CancellationToken token)
        {
            var query = new IndexQuery
            {
                Cutoff = DateTime.UtcNow,
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

            using (var reset = new ManualResetEventSlim(false))
            {
                try
                {
                    token.Register(() => reset.Set());
                    Task.Run(() =>
                    {
                        operation.WaitForCompletion();
                        reset.Set();
                    }, token);
                }
                catch (Exception)
                {
                    reset.Set();
                }
            
                reset.Wait();
            }
            logger.Info("Clean-up of expired audit documents complete.");
        }
    }
}
