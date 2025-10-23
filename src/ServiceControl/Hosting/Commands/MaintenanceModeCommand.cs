namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using Particular.ServiceControl.Hosting;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    sealed class MaintenanceModeCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.SetupApplicationConfiguration();
            hostBuilder.Services.AddPersistence(hostBuilder.Configuration);

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
