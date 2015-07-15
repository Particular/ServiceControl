namespace ServiceControl.Recoverability
{
    using NServiceBus;

    class RetryStartupAndShutdownTasks : IWantToRunWhenBusStartsAndStops
    {
        public void Start()
        {
            if(RetryDocumentManager != null)
                RetryDocumentManager.AdoptOrphanedBatches();
        }

        public void Stop()
        {
            if(Retries != null)
                Retries.StopProcessingOutstandingBatches();
        }

        public RetriesGateway Retries { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }
    }
}