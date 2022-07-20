namespace ServiceControl.Persistence.InMemory
{
    using Microsoft.Extensions.DependencyInjection;

    class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IMonitoringDataStore, InMemoryMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, InMemoryCustomCheckDataStore>();
        }
    }
}
