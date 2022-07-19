namespace ServiceControl.Persistence
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CustomChecks;
    using ServiceControl.Infrastructure.RavenDB;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, Settings settings)
        {
            try
            {
                var documentStore = new EmbeddableDocumentStore();
                RavenBootstrapper.Configure(documentStore, settings);

                var persistenceCreationInfo = GetPersistenceCreationInfo(settings.DataStoreType, settings.SqlStorageConnectionString, documentStore);

                var persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(persistenceCreationInfo.Item1, persistenceCreationInfo.Item2);
                hostBuilder.ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddSingleton<IDocumentStore>(documentStore);
                    serviceCollection.AddHostedService<EmbeddedRavenDbHostedService>();
                    serviceCollection.AddCustomCheck<CheckRavenDBIndexErrors>();
                    serviceCollection.AddCustomCheck<CheckRavenDBIndexLag>();

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
                    return (Type.GetType(DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName, true), new object[1] { new object[0] });
                case DataStoreType.RavenDb:
                    return (Type.GetType(DataStoreConfig.RavenDbPersistenceTypeFullyQualifiedName, true), new object[1] { new object[1] { documentStore } });
                case DataStoreType.SqlDb:
                    return (Type.GetType(DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName, true), new object[1] { new object[1] { sqlConnection } });
                default:
                    return (default, null);
            }
        }
    }
}