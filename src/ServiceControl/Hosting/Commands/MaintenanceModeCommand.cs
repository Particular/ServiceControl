namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
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

            // TODO: Move into the bootstrapper
            hostBuilder.Services.AddWindowsService();

            if (WindowsServiceHelpers.IsWindowsService())
            {
                // IsWindowsService has a platform guard for Windows, so we can safely use it here
                hostBuilder.Services.AddSingleton<IHostLifetime, PersisterInitializingWindowsServiceLifetime>();
            }
            else
            {
                hostBuilder.Services.AddSingleton<IHostLifetime, PersisterInitializingConsoleLifetime>();
            }

            // TODO: Update to use the same pattern as the main Bootstrapper
            using var host = hostBuilder.Build();

            await host.RunAsync();
        }
    }
}
