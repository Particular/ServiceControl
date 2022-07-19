namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Dapper;
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
            try
            {
                var persistence = Type.GetType(DataStoreConfig.InMemoryPersistence, true);
                MonitoringDataStore = ((IPersistenceConfiguration)Activator.CreateInstance(persistence, new object[1] { new object[0] })).MonitoringDataStore;
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {DataStoreConfig.InMemoryPersistence}.", e);
            }

            return Task.CompletedTask;
        }

        async Task SetupSqlDb()
        {
            await SetupSqlPersistence.SetupMonitoring(sqlDbConnectionString).ConfigureAwait(false);

            try
            {
                var persistence = Type.GetType(DataStoreConfig.SqlServerPersistence, true);
                MonitoringDataStore = ((IPersistenceConfiguration)Activator.CreateInstance(persistence, new object[1] { new object[2] { sqlDbConnectionString, new FakeDomainEvents() } })).MonitoringDataStore;
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {DataStoreConfig.SqlServerPersistence}.", e);
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
                var persistence = Type.GetType(DataStoreConfig.RavenDbPersistence, true);
                MonitoringDataStore = ((IPersistenceConfiguration)Activator.CreateInstance(persistence, new object[1] { new object[1] { documentStore } })).MonitoringDataStore;
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {DataStoreConfig.RavenDbPersistence}.", e);
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