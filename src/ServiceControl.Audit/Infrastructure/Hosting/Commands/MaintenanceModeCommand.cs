﻿namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using Persistence;
    using ServiceControl.Hosting;
    using Settings;

    class MaintenanceModeCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings);
            var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);

            persistenceSettings.MaintenanceMode = true;

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Services.AddPersistence(persistenceSettings, persistenceConfiguration);

            if (WindowsServiceHelpers.IsWindowsService())
            {
                // The if is added for clarity, internally AddWindowsService has a similar logic
                hostBuilder.AddWindowsServiceWithRequestTimeout();
            }

            var host = hostBuilder.Build();
            await host.RunAsync();
        }
    }
}