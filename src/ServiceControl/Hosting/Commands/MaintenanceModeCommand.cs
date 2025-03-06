namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
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
