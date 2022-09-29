namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.Hosting;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, PersistenceSettings persistenceSettings)
        {
            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddServiceControlAuditPersistence(persistenceSettings, false);
            });

            return hostBuilder;
        }
    }
}