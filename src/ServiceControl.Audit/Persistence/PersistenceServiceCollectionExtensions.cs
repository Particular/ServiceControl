namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.DependencyInjection;

    static class PersistenceServiceCollectionExtensions
    {
        public static void AddPersistence(this IServiceCollection services,
            PersistenceSettings persistenceSettings,
            IPersistenceConfiguration persistenceConfiguration)
        {
            var persistence = persistenceConfiguration.Create(persistenceSettings);

            var lifecycle = persistence.Configure(services);

            services.AddSingleton(new PersistenceLifecycleHostedService(lifecycle));
            services.AddHostedService(sp => sp.GetRequiredService<PersistenceLifecycleHostedService>());
        }
    }
}