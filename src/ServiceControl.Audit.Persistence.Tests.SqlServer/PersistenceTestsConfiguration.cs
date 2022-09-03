﻿namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Audit.Persistence.SqlServer;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }

        public async Task Configure()
        {

            connectionString = Environment.GetEnvironmentVariable("ServiceControl/SqlStorageConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("No connection string set in environment variable `ServiceControl/SqlStorageConnectionString`");
            }

            await SetupSqlPersistence.SetupAuditTables(connectionString).ConfigureAwait(false);

            var config = new SqlDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var settings = new FakeSettings
            {
                SqlStorageConnectionString = connectionString,
            };

            config.ConfigureServices(serviceCollection, settings, false, true);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
        }

        public Task CompleteDBOperation()
        {
            return Task.CompletedTask;
        }

        public async Task Cleanup()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var dropConstraints = "EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'";
                var dropTables = "EXEC sp_msforeachtable 'DROP TABLE ?'";

                await connection.OpenAsync().ConfigureAwait(false);
                await connection.ExecuteAsync(dropConstraints).ConfigureAwait(false);
                await connection.ExecuteAsync(dropTables).ConfigureAwait(false);
            }
        }

        public override string ToString() => "SqlServer";

        string connectionString;

        class FakeSettings : Settings
        {
            //bypass the public ctor to avoid all mandatory settings
            public FakeSettings() : base()
            {
            }
        }
    }
}