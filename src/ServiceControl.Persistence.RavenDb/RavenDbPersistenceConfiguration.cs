namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Persistence.UnitOfWork;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => throw new System.NotImplementedException();

        public IEnumerable<string> ConfigurationKeys => throw new System.NotImplementedException();

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IMonitoringDataStore, RavenDbMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, RavenDbCustomCheckDataStore>();
            serviceCollection.AddUnitOfWorkFactory<RavenDbIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<MinimumRequiredStorageState>();
        }

        public IPersistence Create(PersistenceSettings settings) => throw new System.NotImplementedException();
    }
}
