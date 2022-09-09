namespace ServiceControl.Audit.Persistence
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    static class PersistenceServiceCollectionExtensions
    {
        public static void AddServiceControlAuditPersistence(this IServiceCollection serviceCollection, Settings settings, bool maintenanceMode = false, bool isSetup = false)
        {
            try
            {
                var persistenceCreationInfo = GetPersistenceCreationInfo(settings.DataStoreType);
                var persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(persistenceCreationInfo);
                persistenceConfig.ConfigureServices(serviceCollection, settings, maintenanceMode, isSetup);

            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization for {settings.DataStoreType}.", e);
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
                case DataStoreType.RavenDb5:
                    return Type.GetType(DataStoreConfig.RavenDb5PersistenceTypeFullyQualifiedName, true);
                case DataStoreType.SqlDb:
                    return Type.GetType(DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName, true);
                default:
                    return default;
            }
        }

    }
}