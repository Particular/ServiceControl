namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using NServiceBus;
    using Settings;
    using WebApi;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.AddServiceControlAudit((_, __) =>
            {
                //Do nothing. The transports in NSB 8 are designed to handle broker outages. Audit ingestion will be paused when broker is unavailable.
                return Task.CompletedTask;
            }, settings, endpointConfiguration);
            hostBuilder.AddServiceControlAuditApi();

            var app = hostBuilder.Build();
            app.UseServiceControlAudit();
            await app.RunAsync(settings.RootUrl);
        }
    }
}