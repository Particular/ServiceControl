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
                var persistenceCreationInfo = GetPersistenceCreationInfo(dataStoreType);
                var persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(persistenceCreationInfo);
                persistenceConfig.ConfigureServices(serviceCollection);

            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization for {dataStoreType}.", e);
            }
        }

        static Type GetPersistenceCreationInfo(DataStoreType dataStoreType)
        {
            return dataStoreType switch
            {
                DataStoreType.InMemory => Type.GetType(DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName, true),
                DataStoreType.RavenDB35 => Type.GetType(DataStoreConfig.RavenDB35PersistenceTypeFullyQualifiedName, true),
                DataStoreType.SqlDb => Type.GetType(DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName, true),
                _ => default,
            };
        }

    }
}