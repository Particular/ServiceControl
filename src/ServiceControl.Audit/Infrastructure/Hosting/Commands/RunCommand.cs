namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using Settings;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            var loggingSettings = new LoggingSettings(settings.ServiceName);

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.AddServiceControlAudit((_, __) =>
            {
                //Do nothing. The transports in NSB 8 are designed to handle broker outages. Audit ingestion will be paused when broker is unavailable.
                return Task.CompletedTask;
            }, settings, endpointConfiguration, loggingSettings);
            var app = hostBuilder.Build();

            app.UseServiceControlAudit();
            await app.RunAsync();
        }
    }
}