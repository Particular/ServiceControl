namespace ServiceControl.Infrastructure.Subscriptions
{
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Persistence;

    class ServiceControlSubscriptionPersistence : PersistenceDefinition
    {
        public ServiceControlSubscriptionPersistence()
        {
            Supports<StorageType.Subscriptions>(s => s.EnableFeatureByDefault<SubscriptionStorage>());
        }
    }
}