namespace ServiceControl.Persistence.InMemory
{
    using Microsoft.Extensions.DependencyInjection;
    using Operations;
    using SqlServer;

    class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IMonitoringDataStore, InMemoryMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, InMemoryCustomCheckDataStore>();
            serviceCollection.AddPartialUnitOfWorkFactory<InMemoryIngestionUnitOfWorkFactory>();
        }
    }
}
