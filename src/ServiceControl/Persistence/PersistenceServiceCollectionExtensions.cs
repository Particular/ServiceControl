namespace ServiceControl.Persistence
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceBus.Management.Infrastructure.Settings;

    [Obsolete("Use PersistenceConfigurationFactory", true)] // TODO: Similar responsibility as PersistenceConfigurationFactory, for now chosen PersistenceConfigurationFactory so this is here to make refactoring easier
    static class PersistenceServiceCollectionExtensions
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public static void AddServiceControlPersistence(this IServiceCollection serviceCollection, DataStoreType dataStoreType)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            try
            {
                //var settings = new PersistenceSettings(TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.FromHours(1), false);

                var persistenceCreationInfo = GetPersistenceCreationInfo(dataStoreType);
                var persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(persistenceCreationInfo);

                //var persistence = persistenceConfig.Create(settings);
                //var lifecycle = persistence.Configure(serviceCollection);

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
                default:
                    return default;
            }
        }

    }
}