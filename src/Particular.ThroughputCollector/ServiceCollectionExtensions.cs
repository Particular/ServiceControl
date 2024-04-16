namespace Particular.ThroughputCollector;

using Microsoft.Extensions.DependencyInjection;
using Particular.ThroughputCollector.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IPersistenceConfiguration persistenceConfiguration)
    {
        var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings();
        var persistence = persistenceConfiguration.Create(persistenceSettings);

        if (!services.IsServiceRegistered(persistence.GetType()))
        {
            persistence.Configure(services);
            services.AddSingleton(persistence);
        }

        return services;
    }

    static bool IsServiceRegistered(this IServiceCollection services, Type serviceType) => services.Any(serviceDescriptor => serviceDescriptor.ServiceType == serviceType);
}
