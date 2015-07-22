namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;

    class RetryStartupAndShutdownTasks : IWantToRunWhenBusStartsAndStops
    {
        public void Start()
        {
            Bus.SendLocal<AdoptOrphanedBatches>(m => m.StartupTime = DateTimeOffset.UtcNow);
        }

        public void Stop()
        {
            if(Retries != null)
                Retries.StopProcessingOutstandingBatches();
        }

        public RetriesGateway Retries { get; set; }
        public IBus Bus { get; set; }
    }
}