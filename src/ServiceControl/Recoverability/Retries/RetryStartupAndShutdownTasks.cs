namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;
    using ServiceControl.Infrastructure;

    class RetryStartupAndShutdownTasks : IWantToRunWhenBusStartsAndStops
    {
        PeriodicExecutor executor;
        PeriodicExecutor retriesGatewayExecutor;

        public RetryStartupAndShutdownTasks()
        {
            executor = new PeriodicExecutor(
                AdoptOrphanedBatches, 
                TimeSpan.FromMinutes(2) 
            );

            retriesGatewayExecutor = new PeriodicExecutor(
                ProcessRequestedBulkRetryOperations, 
                TimeSpan.FromSeconds(5));
        }

        void ProcessRequestedBulkRetryOperations(PeriodicExecutor obj)
        {
            bool processedRequests;
            do
            {
                processedRequests = Retries.ProcessNextBulkRetry();
            } while (processedRequests && !obj.IsCancellationRequested);
        }

        private void AdoptOrphanedBatches(PeriodicExecutor ex)
        {
            var allDone = true;
            if (RetryDocumentManager != null)
            {
                allDone = RetryDocumentManager.AdoptOrphanedBatches();
            }

            if (allDone)
            {
                executor.Stop();
            }
        }

        public void Start()
        {
            if (Retries != null)
            {
                retriesGatewayExecutor.Start(true);
            }
            executor.Start(false);
        }

        public void Stop()
        {
            if (Retries != null)
            {
                retriesGatewayExecutor.Stop();
            }
            executor.Stop();
        }

        public RetriesGateway Retries { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }
    }
}