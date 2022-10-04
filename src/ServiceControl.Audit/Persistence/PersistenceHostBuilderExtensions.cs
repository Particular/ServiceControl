namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, PersistenceSettings persistenceSettings)
        {
            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration();
            var persistence = persistenceConfiguration.Create(persistenceSettings);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                var lifecycle = persistence.CreateLifecycle(serviceCollection);

                serviceCollection.AddHostedService(_ => new PersistenceLifecycleHostedService(lifecycle));
            });

            return hostBuilder;
        }
    }
}