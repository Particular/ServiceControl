namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.Hosting;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, PersistenceSettings persistenceSettings)
        {
            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration();

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                persistenceConfiguration.ConfigureServices(serviceCollection, persistenceSettings);
            });

            return hostBuilder;
        }
    }
}