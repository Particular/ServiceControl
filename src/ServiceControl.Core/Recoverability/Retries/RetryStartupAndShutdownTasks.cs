namespace ServiceControl.Recoverability.Retries
{
    using NServiceBus;

    public class RetryStartupAndShutdownTasks : IWantToRunWhenBusStartsAndStops
    {
        public Retryer Retryer { get; set; }

        public void Start()
        {
            if (Retryer != null)
            {
                Retryer.Start();
            }
        }

        public void Stop()
        {
            if (Retryer != null)
            {
                Retryer.Stop();
            }
        }
    }
}