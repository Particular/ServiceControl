namespace ServiceControl.Audit.Persistence
{
    using Microsoft.Extensions.Hosting;
    using ServiceControl.Audit.Infrastructure.Settings;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, Settings settings, bool maintenanceMode = false)
        {
            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddServiceControlAuditPersistence(settings, maintenanceMode);
            });

            return hostBuilder;
        }
    }
}