namespace ServiceControl.PersistenceTests
{
    using System.Configuration;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    class SqlDb : TestPersistence
    {
        public SqlDb(string sqlDbConnectionString)
        {
            this.sqlDbConnectionString = sqlDbConnectionString;
        }

        public override async Task Configure(IServiceCollection services)
        {
            await SetupSqlPersistence.SetupMonitoring(sqlDbConnectionString);
            await SetupSqlPersistence.SetupCustomChecks(sqlDbConnectionString);

            ConfigurationManager.AppSettings.Set("ServiceControl/SqlStorageConnectionString", sqlDbConnectionString);

            fallback = await services.AddInitializedDocumentStore();
            services.AddSingleton(new Settings() /*{ SqlStorageConnectionString = sqlDbConnectionString }*/);
            services.AddServiceControlPersistence(DataStoreType.SqlDb);
        }

        public override async Task CleanupDB()
        {
            using (var connection = new SqlConnection(sqlDbConnectionString))
            {
                var dropConstraints = "EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'";
                var dropTables = "EXEC sp_msforeachtable 'DROP TABLE ?'";

                await connection.OpenAsync();
                await connection.ExecuteAsync(dropConstraints);
                await connection.ExecuteAsync(dropTables);
            }

            fallback?.Dispose();
        }

        public override string ToString() => "Sql";

        string sqlDbConnectionString;
        EmbeddableDocumentStore fallback;
    }
}