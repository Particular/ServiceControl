namespace Particular.ThroughputCollector.Persistence.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Particular.ThroughputCollector.Persistence;


public class InMemoryPersistence : IPersistence
{
    public InMemoryPersistence(PersistenceSettings persistenceSettings) => settings = persistenceSettings;

    public IPersistenceLifecycle Configure(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<InMemoryThroughputDataStore>();
        serviceCollection.AddSingleton<IThroughputDataStore>(sp => sp.GetRequiredService<InMemoryThroughputDataStore>());

        return new InMemoryPersistenceLifecycle();
    }

    public IPersistenceInstaller CreateInstaller() => new InMemoryPersistenceInstaller();

#pragma warning disable IDE0052 // Remove unread private members
    readonly PersistenceSettings settings;
#pragma warning restore IDE0052 // Remove unread private members
}