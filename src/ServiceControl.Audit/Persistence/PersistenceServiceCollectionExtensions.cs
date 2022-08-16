namespace ServiceControl.Audit.Persistence
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

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
            switch (dataStoreType)
            {
                case DataStoreType.InMemory:
                    return Type.GetType(DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName, true);
                case DataStoreType.RavenDb:
                    return Type.GetType(DataStoreConfig.RavenDbPersistenceTypeFullyQualifiedName, true);
                case DataStoreType.SqlDb:
                    return Type.GetType(DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName, true);
                default:
                    return default;
            }
        }

    }
}