namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using Hosting;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class RunCommand : AbstractCommand
    {
        public override Task Execute(HostArguments args)
        {
            var endpointConfiguration = new EndpointConfiguration(args.ServiceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            var settings = new Settings(args.ServiceName)
            {
                RunCleanupBundle = true,
                Components = Components.All
            };

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

            return hostBuilder.Build().RunAsync();
        }
    }
}