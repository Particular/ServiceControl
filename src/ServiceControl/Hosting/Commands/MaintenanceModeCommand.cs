namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using Particular.ServiceControl.Hosting;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceModeCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Services.AddPersistence(settings, maintenanceMode: true);

            hostBuilder.Services.AddWindowsService();

            if (WindowsServiceHelpers.IsWindowsService())
            {
                hostBuilder.Services.AddSingleton<IHostLifetime, PersisterInitializingWindowsServiceLifetime>();
            }
            else
            {
                hostBuilder.Services.AddSingleton<IHostLifetime, PersisterInitializingConsoleLifetime>();
            }

            // TODO: Update to use the same pattern as the main Bootstrapper
            var host = hostBuilder.Build();

            await host.RunAsync();
        }
    }
}
