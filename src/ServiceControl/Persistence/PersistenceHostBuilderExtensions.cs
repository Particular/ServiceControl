namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder hostBuilder, Settings settings, bool maintenanceMode = false)
        {
            var persistence = PersistenceFactory.Create(settings, maintenanceMode);

            var services = hostBuilder.Services;

            CreatePersisterLifecyle(services, persistence);

            return hostBuilder;
        }

        public static void CreatePersisterLifecyle(IServiceCollection serviceCollection, IPersistence persistence)
        {
            persistence.ConfigureLifecycle(serviceCollection);
            persistence.Configure(serviceCollection);
        }
    }
}
