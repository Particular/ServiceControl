namespace Particular.ThroughputCollector.Persistence.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Particular.ThroughputCollector.Persistence;


public class InMemoryPersistence() : IPersistence
{
    public PersistenceService Configure(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<InMemoryThroughputDataStore>();
        serviceCollection.AddSingleton<IThroughputDataStore>(sp => sp.GetRequiredService<InMemoryThroughputDataStore>());

        return new InMemoryPersistenceLifecycle();
    }

    public IPersistenceInstaller CreateInstaller() => new InMemoryPersistenceInstaller();
}