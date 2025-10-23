namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceServiceCollectionExtensions
    {
        public static void AddPersistence(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            var persistence = PersistenceFactory.Create(configuration);
            persistence.AddPersistence(services, configuration);
        }
    }
}
