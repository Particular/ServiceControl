namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.Hosting;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, PersistenceSettings persistenceSettings, IPersistenceConfiguration persistenceConfiguration)
        {
            hostBuilder.ConfigureServices(serviceCollection =>
            {
                var persistence = persistenceConfiguration.Create(persistenceSettings);
                persistence.Configure(serviceCollection);
            });

            return hostBuilder;
        }
    }
}