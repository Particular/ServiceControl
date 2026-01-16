namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using NServiceBus;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl;
    using ServiceControl.Hosting.Auth;
    using ServiceControl.Hosting.Https;
    using ServicePulse;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            settings.RunCleanupBundle = true;

            var hostBuilder = WebApplication.CreateBuilder();

            hostBuilder.AddServiceControlAuthentication(settings.OpenIdConnectSettings);
            hostBuilder.AddServiceControlHttps(settings.HttpsSettings);
            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi(settings.CorsSettings);

            var app = hostBuilder.Build();
            app.UseServiceControl(settings.ForwardedHeadersSettings, settings.HttpsSettings);
            app.UseServicePulse(ServicePulseSettings.GetFromEnvironmentVariables() with
            {
                ServiceControlUrl = settings.ApiUrl
            });
            app.UseServiceControlAuthentication(settings.OpenIdConnectSettings.Enabled);

            await app.RunAsync(settings.RootUrl);
        }
    }
}
