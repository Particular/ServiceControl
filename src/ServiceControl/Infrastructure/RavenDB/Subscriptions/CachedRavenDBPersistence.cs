namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using NServiceBus.Features;
    using NServiceBus.Persistence;

    public class CachedRavenDBPersistence : PersistenceDefinition
    {
        public CachedRavenDBPersistence()
        {
            Supports<StorageType.Subscriptions>(s => s.EnableFeatureByDefault<SubscriptionStorage>());
        }
    }
}