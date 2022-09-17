namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Microsoft.Extensions.DependencyInjection;
    using SqlServer;
    using UnitOfWork;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using System.Collections.Generic;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

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

            var specificSettings = new Dictionary<string, string>()
            {
                { "Sql/ConnectionString",connectionString}
            };

            var settings = new PersistenceSettings(specificSettings)
            {
                IsSetup = true
            };

            config.ConfigureServices(serviceCollection, settings);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();
            BodyStorage = serviceProvider.GetService<IBodyStorage>();
            AuditIngestionUnitOfWorkFactory = serviceProvider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
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
    }
}
