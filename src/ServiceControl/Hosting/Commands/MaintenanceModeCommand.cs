namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Commands;
    using Particular.ServiceControl.Hosting;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceModeCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var bootstrapper = new MaintenanceBootstrapper(settings);
            var hostBuilder = bootstrapper.HostBuilder;

            // TODO: Move into the bootstrapper
            if (args.RunAsWindowsService)
            {
                hostBuilder.Services.AddWindowsService();
            }
            else
            {
                await Console.Out.WriteLineAsync("RavenDB Maintenance Mode - Press CTRL+C to exit");
            }

            // TODO: Update to use the same pattern as the main Bootstrapper
            using var host = hostBuilder.Build();

            // Initialized IDocumentStore, this is needed as many hosted services have (indirect) dependencies on it.
            await host.Services.GetRequiredService<IPersistenceLifecycle>().Initialize();
            await host.RunAsync();
        }
    }
}
