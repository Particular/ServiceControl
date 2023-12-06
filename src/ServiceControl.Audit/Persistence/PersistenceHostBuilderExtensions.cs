namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostApplicationBuilder SetupPersistence(this IHostApplicationBuilder hostBuilder,
            PersistenceSettings persistenceSettings,
            IPersistenceConfiguration persistenceConfiguration)
        {
            var persistence = persistenceConfiguration.Create(persistenceSettings);

            var services = hostBuilder.Services;
            var lifecycle = persistence.Configure(services);

            services.AddSingleton(new PersistenceLifecycleHostedService(lifecycle));
            services.AddHostedService(sp => sp.GetRequiredService<PersistenceLifecycleHostedService>());
            return hostBuilder;
        }
    }
}