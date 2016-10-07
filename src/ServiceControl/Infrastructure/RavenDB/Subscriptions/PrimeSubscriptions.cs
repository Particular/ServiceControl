namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using NServiceBus;
    using NServiceBus.Config;

    class PrimeSubscriptions : IWantToRunWhenConfigurationIsComplete
    {
        public IPrimableSubscriptionStorage Persister { get; set; }

        public void Run(Configure config)
        {
            Persister?.Prime();
        }
    }
}