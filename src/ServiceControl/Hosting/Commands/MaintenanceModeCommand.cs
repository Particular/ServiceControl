namespace ServiceControl.Hosting.Commands
{
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

            hostBuilder.SetupLifetime(args.RunAsWindowsService);

            using (var host = hostBuilder.Build())
            {
                await host.RunAsync();
            }
        }
    }
}