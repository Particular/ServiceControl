namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using NServiceBus;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.AddServiceControlMonitoring((_, __) => Task.CompletedTask, settings, endpointConfiguration);
            hostBuilder.AddServiceControlMonitoringApi();

            var app = hostBuilder.Build();
            app.UseServiceControlMonitoring();
            await app.RunAsync(settings.RootUrl);
        }
    }
}