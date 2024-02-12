namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using global::ServiceControl;
    using Hosting;
    using Microsoft.AspNetCore.Builder;
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

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.AddServiceControl(settings, endpointConfiguration, loggingSettings);
            var app = hostBuilder.Build();

            app.UseServiceControl();
            await app.StartServiceControl();
            await app.WaitForShutdownAsync();
        }
    }
}
