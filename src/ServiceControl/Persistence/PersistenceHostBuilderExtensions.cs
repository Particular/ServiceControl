namespace ServiceControl.Persistence
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, Settings settings, IDocumentStore documentStore)
        {
            try
            {
                var persistenceCreationInfo = GetPersistenceCreationInfo(settings.DataStoreType, settings.SqlStorageConnectionString, documentStore);

                var persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(persistenceCreationInfo.Item1, persistenceCreationInfo.Item2);
                hostBuilder.ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddSingleton(persistenceConfig.CustomCheckDataStore);
                    serviceCollection.AddSingleton(persistenceConfig.MonitoringDataStore);
                });
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization for {settings.DataStoreType}.", e);
            }

            return hostBuilder;
        }

        static (Type, object[]) GetPersistenceCreationInfo(DataStoreType dataStoreType, string sqlConnection, IDocumentStore documentStore)
        {
            switch (dataStoreType)
            {
                case DataStoreType.InMemory:
                    return (Type.GetType(DataStoreConfig.InMemoryPersistence, true), new object[1] { new object[0] });
                case DataStoreType.RavenDb:
                    return (Type.GetType(DataStoreConfig.RavenDbPersistence, true), new object[1] { new object[1] { documentStore } });
                case DataStoreType.SqlDb:
                    return (Type.GetType(DataStoreConfig.SqlServerPersistence, true), new object[1] { new object[1] { sqlConnection } });
                default:
                    return (default, null);
            }
        }
    }
}