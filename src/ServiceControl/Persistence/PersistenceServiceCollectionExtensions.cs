namespace ServiceControl.Persistence;

using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;

static class PersistenceServiceCollectionExtensions
{
    public static void AddPersistence(this IServiceCollection services, Settings settings, bool maintenanceMode = false)
    {
        var persistence = PersistenceFactory.Create(settings, maintenanceMode);
        persistence.AddPersistence(services);
    }

    public static void AddPersistenceInstallers(this IServiceCollection services, Settings settings)
    {
        var persistence = PersistenceFactory.Create(settings);
        persistence.AddInstaller(services);
    }
}
