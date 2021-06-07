namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using Settings;

    class RunCommand : AbstractCommand
    {
        public override Task Execute(HostArguments args)
        {
            var runAsWindowsService = args.RunAsWindowsService;
            var busConfiguration = new EndpointConfiguration(args.ServiceName);
            var assemblyScanner = busConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            var loggingSettings = new LoggingSettings(args.ServiceName);

            var settings = new Settings(args.ServiceName)
            {
                RunCleanupBundle = true
            };

            var host = new Bootstrapper(
                ctx => { }, //Do nothing. The transports in NSB 7 are designed to handle broker outages. Audit ingestion will be paused when broker is unavailable.
                settings, busConfiguration, loggingSettings).HostBuilder;

            if (runAsWindowsService)
            {
                host.UseWindowsService();
            }
            else
            {
                host.UseConsoleLifetime();
            }

            return host.Build().RunAsync();
        }
    }
}