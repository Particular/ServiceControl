namespace ServiceControl.Recoverability.Retries
{
    using NServiceBus;

    public class RetryStartupAndShutdownTasks : IWantToRunWhenBusStartsAndStops
    {
        public Retryer Retryer { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }

        public void Start()
        {
            if (RetryDocumentManager != null)
            {
                RetryDocumentManager.AdoptOrphanedBatches();
            }
        }

        public void Stop()
        {
            if (Retryer != null)
            {
                Retryer.StopProcessingOutstandingBatches();
            }
        }
    }
}