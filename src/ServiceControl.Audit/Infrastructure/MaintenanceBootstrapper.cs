namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using Hosting;
    using Microsoft.Extensions.Hosting;
    using ServiceControl.Audit.Persistence;

    static class MaintenanceBootstrapper
    {
        public static async Task Run(HostArguments args, Settings.Settings settings)
        {
            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings.PersistenceType);
            var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);

            persistenceSettings.MaintenanceMode = true;

            var hostBuilder = new HostBuilder()
                .SetupPersistence(persistenceSettings, persistenceConfiguration);

            if (args.RunAsWindowsService)
            {
                hostBuilder.UseWindowsService();

                await hostBuilder.Build().RunAsync();
            }
            else
            {
                hostBuilder.UseConsoleLifetime();

                await Console.Out.WriteLineAsync("Running in Maintenance Mode - Press CTRL+C to exit");

                await hostBuilder.Build().RunAsync();

                await Console.Out.WriteLineAsync("Disposing persister (this might take a while)...");
            }
        }
    }
}