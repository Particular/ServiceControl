namespace Particular.ThroughputCollector;

using Microsoft.Extensions.DependencyInjection;

static class ServiceCollectionExtensions
{
    /// <remarks>
    /// It is possible for multiple different hosts to be created by Service Control and its associated test infrastructure,
    /// which means AddPersistence can be called multiple times and potentially with different persistence types
    /// </remarks>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string persistenceType, string persistenceAssembly)
    {
        var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(persistenceType, persistenceAssembly);
        var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings();
        services.AddSingleton(persistenceSettings);

        var persistence = persistenceConfiguration.Create(persistenceSettings);
        persistence.Configure(services);
        services.AddSingleton(persistence);

        return services;
    }
}
