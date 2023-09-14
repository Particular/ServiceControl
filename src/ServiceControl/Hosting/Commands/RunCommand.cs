namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using global::ServiceControl.Persistence;
    using Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(args.ServiceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            settings.RunCleanupBundle = true;

            var loggingSettings = new LoggingSettings(args.ServiceName);

            var bootstrapper = new Bootstrapper(settings, endpointConfiguration, loggingSettings);
            var hostBuilder = bootstrapper.HostBuilder;

            if (args.RunAsWindowsService)
            {
                hostBuilder.UseWindowsService();
            }
            else
            {
                hostBuilder.UseConsoleLifetime();
            }

            var host = hostBuilder.Build();

            var lifeCycle = host.Services.GetRequiredService<IPersistenceLifecycle>();
            await lifeCycle.Start(); // Initialized IDocumentStore, this is needed as many hosted services have (indirect) dependencies on it.
            try
            {
                await host.RunAsync();
            }
            finally
            {
                await lifeCycle.Stop();
            }
        }
    }
}