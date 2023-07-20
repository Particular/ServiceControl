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
            await SetupSqlPersistence.SetupMonitoring(sqlDbConnectionString).ConfigureAwait(false);
            await SetupSqlPersistence.SetupCustomChecks(sqlDbConnectionString).ConfigureAwait(false);

            ConfigurationManager.AppSettings.Set("ServiceControl/SqlStorageConnectionString", sqlDbConnectionString);

            fallback = await services.AddInitializedDocumentStore().ConfigureAwait(false);
            services.AddSingleton(new Settings() /*{ SqlStorageConnectionString = sqlDbConnectionString }*/);
            services.AddServiceControlPersistence(DataStoreType.SqlDb);
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