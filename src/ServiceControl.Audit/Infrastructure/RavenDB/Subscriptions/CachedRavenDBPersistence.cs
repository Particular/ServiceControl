namespace ServiceControl.Audit.Infrastructure.RavenDB.Subscriptions
{
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Persistence;

    class CachedRavenDBPersistence : PersistenceDefinition
    {
        public CachedRavenDBPersistence()
        {
            Supports<StorageType.Subscriptions>(s => s.EnableFeatureByDefault<SubscriptionStorage>());
        }
    }
}