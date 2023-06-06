﻿namespace ServiceControl.PersistenceTests
{
    using System.Configuration;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Persistence.Tests;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.UnitOfWork;

    class SqlDb : PersistenceDataStoreFixture
    {
        public SqlDb(string sqlDbConnectionString)
        {
            this.sqlDbConnectionString = sqlDbConnectionString;
        }

        public override async Task SetupDataStore()
        {
            await SetupSqlPersistence.SetupMonitoring(sqlDbConnectionString).ConfigureAwait(false);
            await SetupSqlPersistence.SetupCustomChecks(sqlDbConnectionString).ConfigureAwait(false);

            ConfigurationManager.AppSettings.Set("ServiceControl/SqlStorageConnectionString", sqlDbConnectionString);

            var serviceCollection = new ServiceCollection();
            fallback = await serviceCollection.AddInitializedDocumentStore().ConfigureAwait(false);
            serviceCollection.AddSingleton(new Settings() /*{ SqlStorageConnectionString = sqlDbConnectionString }*/);
            serviceCollection.AddServiceControlPersistence(DataStoreType.SqlDb);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
            CustomCheckDataStore = serviceProvider.GetRequiredService<ICustomChecksDataStore>();
            UnitOfWorkFactory = serviceProvider.GetRequiredService<IIngestionUnitOfWorkFactory>();
        }

        public override async Task CleanupDB()
        {
            using (var connection = new SqlConnection(sqlDbConnectionString))
            {
                var dropConstraints = "EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'";
                var dropTables = "EXEC sp_msforeachtable 'DROP TABLE ?'";

                await connection.OpenAsync().ConfigureAwait(false);
                await connection.ExecuteAsync(dropConstraints).ConfigureAwait(false);
                await connection.ExecuteAsync(dropTables).ConfigureAwait(false);
            }

            fallback?.Dispose();
        }

        public override string ToString() => "Sql";

        string sqlDbConnectionString;
        EmbeddableDocumentStore fallback;
    }
}