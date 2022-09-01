namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }

        public async Task Configure()
        {
            connectionString = SettingsReader<string>.Read("ServiceControl.Audit", "SqlStorageConnectionString", "");
            await SetupSqlPersistence.SetupAuditTables(connectionString).ConfigureAwait(false);

            var settings = new Settings
            {
                SqlStorageConnectionString = connectionString,
                DataStoreType = DataStoreType.SqlDb
            };
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(settings);
            serviceCollection.AddServiceControlAuditPersistence(settings);
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
    }
}