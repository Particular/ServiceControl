namespace Particular.ThroughputCollector.Persistence.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Particular.ThroughputCollector.Persistence;


public class InMemoryPersistence() : IPersistence
{
    public IServiceCollection Configure(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<InMemoryThroughputDataStore>();
        serviceCollection.AddSingleton<IThroughputDataStore>(sp => sp.GetRequiredService<InMemoryThroughputDataStore>());
        serviceCollection.AddSingleton<IPersistenceInstaller, InMemoryPersistenceInstaller>();

        return serviceCollection;
    }
}