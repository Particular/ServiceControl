namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
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

            var host = hostBuilder.Build();
            await host.RunAsync();
        }
    }
}