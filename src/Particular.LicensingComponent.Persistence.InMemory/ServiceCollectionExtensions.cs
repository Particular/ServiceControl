namespace Particular.LicensingComponent.Persistence.InMemory;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLicensingInMemoryPersistence(this IServiceCollection services)
    {
        services.AddSingleton<ILicensingDataStore, InMemoryLicensingDataStore>();

        return services;
    }
}