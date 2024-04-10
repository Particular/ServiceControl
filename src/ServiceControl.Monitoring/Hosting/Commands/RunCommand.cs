namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using NServiceBus;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.ServiceName);

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.AddServiceControlMonitoring((_, __) => Task.CompletedTask, settings, endpointConfiguration);
            hostBuilder.AddServiceControlMonitoringApi();

            await using var app = hostBuilder.Build();
            app.UseServiceControlMonitoring();
            await app.RunAsync(settings.RootUrl);
        }
    }
}