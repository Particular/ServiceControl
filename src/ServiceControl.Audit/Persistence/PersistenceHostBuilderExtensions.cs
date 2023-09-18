namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder,
            PersistenceSettings persistenceSettings,
            IPersistenceConfiguration persistenceConfiguration)
        {
            var persistence = persistenceConfiguration.Create(persistenceSettings);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                var lifecycle = persistence.Configure(serviceCollection);

                serviceCollection.AddSingleton(new PersistenceLifecycleHostedService(lifecycle));
                serviceCollection.AddHostedService(sp => sp.GetRequiredService<PersistenceLifecycleHostedService>());
            });

            return hostBuilder;
        }
    }
}