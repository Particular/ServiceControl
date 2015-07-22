namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;
    using ServiceControl.Infrastructure;

    class RetryStartupAndShutdownTasks : IWantToRunWhenBusStartsAndStops
    {
        PeriodicExecutor executor;

        public RetryStartupAndShutdownTasks()
        {
            executor = new PeriodicExecutor(
                AdoptOrphanedBatches, 
                TimeSpan.FromMinutes(2) 
            );
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
            executor.Start(false);
        }

        public void Stop()
        {
            if (Retries != null)
            {
                Retries.StopProcessingOutstandingBatches();
            }
            executor.Stop();
        }

        public RetriesGateway Retries { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }
    }
}