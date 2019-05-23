﻿namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
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