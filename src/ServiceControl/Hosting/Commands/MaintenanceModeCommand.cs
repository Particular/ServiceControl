namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Commands;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceModeCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var bootstrapper = new MaintenanceBootstrapper(settings);
            var hostBuilder = bootstrapper.HostBuilder;

            if (args.RunAsWindowsService)
            {
                hostBuilder.UseWindowsService();
            }
            else
            {
                await Console.Out.WriteLineAsync($"RavenDB is now accepting requests on {settings.DatabaseMaintenanceUrl}");
                await Console.Out.WriteLineAsync("RavenDB Maintenance Mode - Press CTRL+C to exit");

                hostBuilder.UseConsoleLifetime();
            }

            await hostBuilder.Build().RunAsync();

        }
    }
}