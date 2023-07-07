namespace ServiceControl.Persistence.InMemory
{
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Persistence.UnitOfWork;

    public class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IMonitoringDataStore, InMemoryMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, InMemoryCustomCheckDataStore>();
            serviceCollection.AddPartialUnitOfWorkFactory<InMemoryIngestionUnitOfWorkFactory>();
        }

        public string Name { get; }
        public IEnumerable<string> ConfigurationKeys { get; }
        public IPersistence Create(PersistenceSettings settings) => throw new System.NotImplementedException();
    }
}
