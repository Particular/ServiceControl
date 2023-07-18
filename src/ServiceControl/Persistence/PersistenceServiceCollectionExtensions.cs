namespace ServiceControl.Persistence
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceServiceCollectionExtensions
    {
        public static void AddServiceControlPersistence(this IServiceCollection serviceCollection, DataStoreType dataStoreType)
        {
            try
            {
                // TODO: This is just to make it compile, values are random
                var settings = new PersistenceSettings(TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.FromHours(1), false);

                var persistenceCreationInfo = GetPersistenceCreationInfo(dataStoreType);
                var persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(persistenceCreationInfo);

                var persistence = persistenceConfig.Create(settings);
                var lifecycle = persistence.Configure(serviceCollection);

                // TODO: Something probably needs to happen with the lifecycle in order to run the tests - look in Audit instance for inspiration

            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization for {dataStoreType}.", e);
            }
        }

        static Type GetPersistenceCreationInfo(DataStoreType dataStoreType)
        {
            switch (dataStoreType)
            {
                case DataStoreType.InMemory:
                    return Type.GetType(DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName, true);
                case DataStoreType.RavenDB35:
                    return Type.GetType(DataStoreConfig.RavenDB35PersistenceTypeFullyQualifiedName, true);
                case DataStoreType.SqlDb:
                    return Type.GetType(DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName, true);
                default:
                    return default;
            }
        }

    }
}