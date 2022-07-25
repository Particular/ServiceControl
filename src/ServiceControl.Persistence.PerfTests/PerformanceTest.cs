namespace ServiceControl.Persistence.PerfTests
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Microsoft.Extensions.DependencyInjection;
    using NBench;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class PerformanceTest
    {
        [PerfSetup]
        public void Setup(BenchmarkContext context)
        {
            SetupTest(context).GetAwaiter().GetResult();
        }

        [PerfCleanup]
        public void PerfCleanup() => CleanupSqlDb().GetAwaiter().GetResult();

        protected virtual async Task SetupTest(BenchmarkContext context)
        {
            await SetupSqlPersistence.SetupMonitoring(sqlDbConnectionString).ConfigureAwait(false);
            await SetupSqlPersistence.SetupCustomChecks(sqlDbConnectionString).ConfigureAwait(false);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(new Settings
            {
                SqlStorageConnectionString = sqlDbConnectionString,
                TransportCustomizationType = "ServiceControl.Transports.Learning.LearningTransportCustomization, ServiceControl.Transports.Learning"
            });
            serviceCollection.AddServiceControlPersistence(DataStoreType.SqlDb);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
            CustomCheckDataStore = serviceProvider.GetRequiredService<ICustomChecksDataStore>();
        }

        protected virtual async Task CleanupSqlDb()
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

        string sqlDbConnectionString = SettingsReader<string>.Read("SqlStorageConnectionString");
        protected ICustomChecksDataStore CustomCheckDataStore { get; private set; }
        protected IMonitoringDataStore MonitoringDataStore { get; private set; }
    }
}