namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using SqlServer;
    using UnitOfWork;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public async Task Configure(Action<PersistenceSettings> setSettings)
        {
            connectionString = Environment.GetEnvironmentVariable("ServiceControl/SqlStorageConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("No connection string set in environment variable `ServiceControl/SqlStorageConnectionString`");
            }

            await SetupSqlPersistence.SetupAuditTables(connectionString).ConfigureAwait(false);

            var config = new SqlDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var settings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);

            setSettings(settings);

            settings.PersisterSpecificSettings["Sql/ConnectionString"] = connectionString;

            config.ConfigureServices(serviceCollection, settings);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();
            BodyStorage = serviceProvider.GetService<IBodyStorage>();
            AuditIngestionUnitOfWorkFactory = serviceProvider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
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

        public Task CompleteDBOperation()
        {
            return Task.CompletedTask;
        }

        public string Name => "SqlServer";

        string connectionString;
    }
}
