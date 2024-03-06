namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl;

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
            using var app = hostBuilder.Build();

            app.UseServiceControl();
            await app.StartServiceControl();
            await app.WaitForShutdownAsync();
        }
    }
}
