namespace ServiceControl.Infrastructure.RavenDB
{
    using NServiceBus;
    using NServiceBus.Persistence;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;

    public class RavenPersistenceConfigurator : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            configuration.UsePersistence<CachedRavenDBPersistence, StorageType.Subscriptions>();
        }
    }
}