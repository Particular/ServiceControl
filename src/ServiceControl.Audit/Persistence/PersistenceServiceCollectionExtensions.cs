namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    static class PersistenceServiceCollectionExtensions
    {
        public static void AddPersistence(this IServiceCollection services,
            PersistenceSettings persistenceSettings,
            IPersistenceConfiguration persistenceConfiguration,
            IConfiguration configuration
        )
        {
            var persistence = persistenceConfiguration.Create(persistenceSettings, configuration);
            persistence.AddPersistence(services);
        }

        public static void AddInstaller(this IServiceCollection services,
            PersistenceSettings persistenceSettings,
            IPersistenceConfiguration persistenceConfiguration,
            IConfiguration configuration
        )
        {
            var persistence = persistenceConfiguration.Create(persistenceSettings, configuration);
            persistence.AddInstaller(services);
        }
    }
}