namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Dapper;
    using Microsoft.Extensions.DependencyInjection;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.RavenDB;
    using ServiceControl.Persistence;

    class MonitoringDataStoreTester
    {
        DataStoreType dataStoreType;
        string sqlDbConnectionString = SettingsReader<string>.Read("SqlStorageConnectionString");
        EmbeddableDocumentStore documentStore;
        public IMonitoringDataStore MonitoringDataStore { get; internal set; }

        public MonitoringDataStoreTester(DataStoreType DatastoreType)
        {
            dataStoreType = DatastoreType;
        }

        public async Task SetupDataStore()
        {
            switch (dataStoreType)
            {
                case DataStoreType.InMemory:
                    await SetupInMemory().ConfigureAwait(false);
                    break;
                case DataStoreType.RavenDb:
                    await SetupRavenDb().ConfigureAwait(false);
                    break;
                case DataStoreType.SqlDb:
                    await SetupSqlDb().ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }

        public Task CompleteDBOperation()
        {
            if (dataStoreType == DataStoreType.RavenDb && documentStore != null)
            {
                documentStore.WaitForIndexing();
            }

            return Task.CompletedTask;
        }

        public async Task CleanupDB()
        {
            switch (dataStoreType)
            {
                case DataStoreType.InMemory:
                    break;
                case DataStoreType.RavenDb:
                    await CleanupRavenDb().ConfigureAwait(false);
                    break;
                case DataStoreType.SqlDb:
                    await CleanupSqlDb().ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }

        Task SetupInMemory()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddServiceControlPersistence(DataStoreType.InMemory);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();

            return Task.CompletedTask;
        }

        async Task SetupSqlDb()
        {
            await SetupSqlPersistence.SetupMonitoring(sqlDbConnectionString).ConfigureAwait(false);

            try
            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(new Settings { SqlStorageConnectionString = sqlDbConnectionString });
                serviceCollection.AddServiceControlPersistence(DataStoreType.SqlDb);
                var serviceProvider = serviceCollection.BuildServiceProvider();
                MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName}.", e);
            }
        }

        async Task CleanupSqlDb()
        {
            using (var connection = new SqlConnection(sqlDbConnectionString))
            {
                var catalog = new SqlConnectionStringBuilder(sqlDbConnectionString).InitialCatalog;

                var truncateCommand = $@"
                    IF EXISTS (
                         SELECT *
                         FROM {catalog}.sys.objects
                         WHERE object_id = OBJECT_ID(N'KnownEndpoints') AND type in (N'U')
                       )
                       BEGIN
                           Drop TABLE [dbo].[KnownEndpoints]
                       END";

                connection.Open();

                await connection.ExecuteAsync(truncateCommand).ConfigureAwait(false);
            }
        }

        async Task SetupRavenDb()
        {
            var settings = new Settings()
            {
                RunInMemory = true,
            };
            documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings);
            documentStore.Initialize();

            ExportProvider CreateIndexProvider(System.Collections.Generic.List<Assembly> indexAssemblies) =>
                new CompositionContainer(
                    new AggregateCatalog(
                        from indexAssembly in indexAssemblies select new AssemblyCatalog(indexAssembly)
                    )
            );

            var indexProvider = CreateIndexProvider(new System.Collections.Generic.List<Assembly>() { typeof(RavenBootstrapper).Assembly });
            await IndexCreation.CreateIndexesAsync(indexProvider, documentStore)
                .ConfigureAwait(false);

            try
            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton<IDocumentStore>(documentStore);
                serviceCollection.AddServiceControlPersistence(DataStoreType.RavenDb);
                var serviceProvider = serviceCollection.BuildServiceProvider();
                MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {DataStoreConfig.RavenDbPersistenceTypeFullyQualifiedName}.", e);
            }
        }

        Task CleanupRavenDb()
        {
            if (documentStore != null)
            {
                documentStore.Dispose();
            }

            return Task.CompletedTask;
        }
    }
}