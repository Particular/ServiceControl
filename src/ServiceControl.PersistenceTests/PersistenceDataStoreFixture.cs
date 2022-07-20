namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using CustomChecks;
    using Dapper;
    using Microsoft.Extensions.DependencyInjection;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.RavenDB;
    using ServiceControl.Persistence;

    class PersistenceDataStoreFixture
    {
        public ICustomChecksDataStore CustomCheckDataStore { get; private set; }
        public IMonitoringDataStore MonitoringDataStore { get; private set; }

        public PersistenceDataStoreFixture(DataStoreType dataStoreType)
        {
            this.dataStoreType = dataStoreType;
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
<<<<<<< HEAD
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddServiceControlPersistence(DataStoreType.InMemory);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
=======
            try
            {
                var persistence = Type.GetType(DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName, true);
                var persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(persistence, new object[] { new object[0] });

                MonitoringDataStore = persistenceConfig.MonitoringDataStore;
                CustomCheckDataStore = persistenceConfig.CustomCheckDataStore;
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName}.", e);
            }
>>>>>>> 052a88a5 (Wire up customcheck persistence for the test)

            return Task.CompletedTask;
        }

        async Task SetupSqlDb()
        {
            await SetupSqlPersistence.SetupMonitoring(sqlDbConnectionString).ConfigureAwait(false);
            await SetupSqlPersistence.SetupCustomChecks(sqlDbConnectionString).ConfigureAwait(false);

            try
            {
<<<<<<< HEAD
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(new Settings { SqlStorageConnectionString = sqlDbConnectionString });
                serviceCollection.AddServiceControlPersistence(DataStoreType.SqlDb);
                var serviceProvider = serviceCollection.BuildServiceProvider();
                MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
=======
                var persistence = Type.GetType(DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName, true);
                var persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(persistence, new object[] { new object[] { sqlDbConnectionString, new FakeDomainEvents() } });

                MonitoringDataStore = persistenceConfig.MonitoringDataStore;
                CustomCheckDataStore = persistenceConfig.CustomCheckDataStore;
>>>>>>> 052a88a5 (Wire up customcheck persistence for the test)
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
                var dropConstraints = "EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'";
                var dropTables = "EXEC sp_msforeachtable 'DROP TABLE ?'";

                await connection.OpenAsync().ConfigureAwait(false);
                await connection.ExecuteAsync(dropConstraints).ConfigureAwait(false);
                await connection.ExecuteAsync(dropTables).ConfigureAwait(false);
            }
        }

        async Task SetupRavenDb()
        {
            var settings = new Settings
            {
                RunInMemory = true
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

            var indexProvider = CreateIndexProvider(new System.Collections.Generic.List<Assembly> { typeof(RavenBootstrapper).Assembly });
            await IndexCreation.CreateIndexesAsync(indexProvider, documentStore)
                .ConfigureAwait(false);

            try
            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton<IDocumentStore>(documentStore);
                serviceCollection.AddServiceControlPersistence(DataStoreType.RavenDb);
                var serviceProvider = serviceCollection.BuildServiceProvider();
                MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
                CustomCheckDataStore = serviceProvider.GetRequiredService<ICustomChecksDataStore>();
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

        public override string ToString() => dataStoreType.ToString();

        DataStoreType dataStoreType;
        EmbeddableDocumentStore documentStore;
        string sqlDbConnectionString = SettingsReader<string>.Read("SqlStorageConnectionString");
    }
}