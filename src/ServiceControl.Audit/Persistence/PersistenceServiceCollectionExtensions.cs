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
            persistence.Configure(services);

            services.AddHostedService<PersistenceLifecycleHostedService>();
        }
    }
}