namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using ServiceControl.Audit.Persistence;

    static class MaintenanceBootstrapper
    {
        public static async Task Run(Settings.Settings settings)
        {
            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings.PersistenceType);
            var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);

            persistenceSettings.MaintenanceMode = true;

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Services.AddPersistence(persistenceSettings, persistenceConfiguration);

            if (WindowsServiceHelpers.IsWindowsService())
            {
                await hostBuilder.Build().RunAsync();
            }
            else
            {
                await Console.Out.WriteLineAsync("Running in Maintenance Mode - Press CTRL+C to exit");

                await hostBuilder.Build().RunAsync();

                await Console.Out.WriteLineAsync("Disposing persister (this might take a while)...");
            }
        }
    }
}