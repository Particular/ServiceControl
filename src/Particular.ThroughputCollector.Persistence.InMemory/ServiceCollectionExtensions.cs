namespace Particular.ThroughputCollector.Persistence.InMemory;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddThroughputInMemoryPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IThroughputDataStore, InMemoryThroughputDataStore>();
        services.AddSingleton<IPersistenceInstaller, InMemoryPersistenceInstaller>();

        return services;
    }
}