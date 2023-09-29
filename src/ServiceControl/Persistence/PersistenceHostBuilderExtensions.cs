namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, Settings settings, bool maintenanceMode = false)
        {
            var persistence = PersistenceFactory.Create(settings, maintenanceMode);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                CreatePersisterLifecyle(serviceCollection, persistence);
            });

            return hostBuilder;
        }

        public static void CreatePersisterLifecyle(IServiceCollection serviceCollection, IPersistence persistence)
        {
            persistence.ConfigureLifecycle(serviceCollection);
            persistence.Configure(serviceCollection);
        }
    }
}
