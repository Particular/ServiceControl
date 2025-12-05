namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using NServiceBus;
    using ServiceControl.Hosting.Auth;
    using ServiceControl.Hosting.Https;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.AddServiceControlAuthentication(settings.OpenIdConnectSettings);
            hostBuilder.AddServiceControlHttps(settings.HttpsSettings);
            hostBuilder.AddServiceControlMonitoring((_, __) => Task.CompletedTask, settings, endpointConfiguration);
            hostBuilder.AddServiceControlMonitoringApi();

            var app = hostBuilder.Build();
            app.UseServiceControlMonitoring(settings.ForwardedHeadersSettings, settings.HttpsSettings, settings.CorsSettings);
            app.UseServiceControlAuthentication(authenticationEnabled: settings.OpenIdConnectSettings.Enabled);

            await app.RunAsync(settings.RootUrl);
        }
    }
}