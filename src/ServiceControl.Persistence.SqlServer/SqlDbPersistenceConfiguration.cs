namespace ServiceControl.Persistence.SqlServer
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Persistence.UnitOfWork;

    public class SqlDbPersistenceConfiguration : IPersistenceConfiguration
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
