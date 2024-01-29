namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceServiceCollectionExtensions
    {
        public static void AddPersistence(this IServiceCollection services, Settings settings,
            bool maintenanceMode = false)
        {
            var persistence = PersistenceFactory.Create(settings, maintenanceMode);

            services.ConfigurePersisterLifecyle(persistence);
        }

        public static void ConfigurePersisterLifecyle(this IServiceCollection serviceCollection, IPersistence persistence)
        {
            persistence.ConfigureLifecycle(serviceCollection);
            persistence.Configure(serviceCollection);
        }
    }
}
