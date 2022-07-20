namespace ServiceControl.Persistence.SqlServer
{
    using Microsoft.Extensions.DependencyInjection;
    using Operations;
    using ServiceBus.Management.Infrastructure.Settings;

    class SqlDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(sp =>
                new SqlDbConnectionManager(sp.GetRequiredService<Settings>().SqlStorageConnectionString));
            serviceCollection.AddSingleton<IMonitoringDataStore, SqlDbMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, SqlDbCustomCheckDataStore>();
            serviceCollection.AddPartialUnitOfWorkFactory<SqlIngestionUnitOfWorkFactory>();
        }
    }
}
