namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public static class SagaHistoryCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(SagaHistoryCleaner));

        public static void Clean(IDocumentStore store, DateTime expiryThreshold, CancellationToken token)
        {
            var query = new IndexQuery
            {
                DisableCaching = true,
                Cutoff = DateTime.UtcNow,
                Query = $"LastModified:[* TO {expiryThreshold.Ticks}]",
            };

            var indexName = new ExpirySagaAuditIndex().IndexName;

            logger.Info("Starting clean-up of expired sagahistory documents.");
            var operation = store.DatabaseCommands.DeleteByIndex(indexName, query, new BulkOperationOptions
            {
                AllowStale = true,
                RetrieveDetails = false,
                MaxOpsPerSec = 700
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
            logger.Info("Clean-up of expired sagahistory documents complete.");
        }
    }
}