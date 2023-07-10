namespace Particular.ServiceControl
{
    using System;
    using System.Threading.Tasks;
    using global::ServiceControl.Persistence;
    using Hosting;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    static class MaintenanceBootstrapper
    {
        public static async Task Run(HostArguments args, Settings settings)
        {
            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings.PersistenceType);
            var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);

            persistenceSettings.MaintenanceMode = true;

            var hostBuilder = new HostBuilder()
                .SetupPersistence(persistenceSettings, persistenceConfiguration);

            if (args.RunAsWindowsService)
            {
                hostBuilder.UseWindowsService();

                await hostBuilder.Build().RunAsync().ConfigureAwait(false);
            }
            else
            {
                await Console.Out.WriteLineAsync($"RavenDB is now accepting requests on {settings.DatabaseMaintenanceUrl}").ConfigureAwait(false);
                await Console.Out.WriteLineAsync("RavenDB Maintenance Mode - Press CTRL+C to exit").ConfigureAwait(false);

                hostBuilder.UseConsoleLifetime();

                await hostBuilder.Build().RunAsync().ConfigureAwait(false);
            }
        }
    }
}