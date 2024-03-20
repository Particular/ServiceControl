namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using Persistence;
    using ServiceControl.Audit.Infrastructure.Settings;

    class MaintenanceModeCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings.PersistenceType);
            var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);

            persistenceSettings.MaintenanceMode = true;

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Services.AddPersistence(persistenceSettings, persistenceConfiguration);

            var host = hostBuilder.Build();

            if (WindowsServiceHelpers.IsWindowsService())
            {
                await host.RunAsync();
            }
            else
            {
                await Console.Out.WriteLineAsync("Running in Maintenance Mode - Press CTRL+C to exit");

                await host.RunAsync();

                await Console.Out.WriteLineAsync("Disposing persister (this might take a while)...");
            }
        }
    }
}