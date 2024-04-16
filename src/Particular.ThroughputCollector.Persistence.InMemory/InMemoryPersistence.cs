namespace Particular.ThroughputCollector.Persistence.InMemory;

using Microsoft.Extensions.DependencyInjection;

public class InMemoryPersistence
{
    public IServiceCollection Configure(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IThroughputDataStore, InMemoryThroughputDataStore>();
        serviceCollection.AddSingleton<IPersistenceInstaller, InMemoryPersistenceInstaller>();

        return serviceCollection;
    }
}