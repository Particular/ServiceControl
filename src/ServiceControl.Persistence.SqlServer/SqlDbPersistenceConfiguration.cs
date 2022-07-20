namespace ServiceControl.Persistence.SqlServer
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceBus.Management.Infrastructure.Settings;

    class SqlDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IMonitoringDataStore>(sp =>
                new SqlDbMonitoringDataStore(sp.GetRequiredService<Settings>().SqlStorageConnectionString));
            serviceCollection.AddSingleton<ICustomChecksDataStore>(sp =>
                new SqlDbCustomCheckDataStore(sp.GetRequiredService<Settings>().SqlStorageConnectionString));
        }
    }
}
