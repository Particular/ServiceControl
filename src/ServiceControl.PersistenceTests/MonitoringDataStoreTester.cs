namespace ServiceControl.Persistence.Tests
{
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
    using ServiceControl.Monitoring;

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
                    MonitoringDataStore = new InMemoryMonitoringDataStore();
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

        async Task SetupSqlDb()
        {
            using (var connection = new SqlConnection(sqlDbConnectionString))
            {
                var catalog = new SqlConnectionStringBuilder(sqlDbConnectionString).InitialCatalog;

                var setupCommand = $@"
                    IF NOT EXISTS (
                         SELECT *
                         FROM {catalog}.sys.objects
                         WHERE object_id = OBJECT_ID(N'KnownEndpoints') AND type in (N'U')
                       )
                       BEGIN
                           CREATE TABLE [dbo].[KnownEndpoints](
                            [Id] [uniqueidentifier] NOT NULL,
                            [HostId] [uniqueidentifier] NOT NULL,
                            [Host] [nvarchar](300) NULL,
                            [HostDisplayName] [nvarchar](300) NULL,
                            [Monitored] [bit] NOT NULL
                           ) ON [PRIMARY]
                       END";

                connection.Open();

                await connection.ExecuteAsync(setupCommand).ConfigureAwait(false);

                MonitoringDataStore = new SqlDbMonitoringDataStore(sqlDbConnectionString);
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

            MonitoringDataStore = new RavenDbMonitoringDataStore(documentStore);
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