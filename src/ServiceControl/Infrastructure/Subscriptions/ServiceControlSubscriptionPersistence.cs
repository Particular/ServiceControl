namespace ServiceControl.Infrastructure.Subscriptions
{
    using NServiceBus;
    using NServiceBus.Persistence;

    class ServiceControlSubscriptionPersistence : PersistenceDefinition, IPersistenceDefinitionFactory<ServiceControlSubscriptionPersistence>
    {
        ServiceControlSubscriptionPersistence() => Supports<StorageType.Subscriptions, SubscriptionStorage>();

        static ServiceControlSubscriptionPersistence IPersistenceDefinitionFactory<ServiceControlSubscriptionPersistence>.Create() => new();
    }
}