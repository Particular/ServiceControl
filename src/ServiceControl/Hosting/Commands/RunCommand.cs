namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using Hosting;
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

            hostBuilder.AddPersistenceInitializingLifetime(args.RunAsWindowsService);

            using (var host = hostBuilder.Build())
            {
                await host.RunAsync();
            }
        }
    }
}