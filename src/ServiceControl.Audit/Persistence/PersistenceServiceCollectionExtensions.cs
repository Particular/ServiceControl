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
            persistence.AddPersistence(services);
        }

        public static void AddInstaller(this IServiceCollection services,
            PersistenceSettings persistenceSettings,
            IPersistenceConfiguration persistenceConfiguration)
        {
            var persistence = persistenceConfiguration.Create(persistenceSettings);
            persistence.AddInstaller(services);
        }
    }
}