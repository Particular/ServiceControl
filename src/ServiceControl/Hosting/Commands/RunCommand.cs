namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl;
    using ServiceControl.Configuration;
    using ServiceControl.Infrastructure.Settings;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            settings.RunCleanupBundle = true;

            var hostBuilder = WebApplication.CreateBuilder();

            hostBuilder.Configuration.Add<AppConfigConfigurationSource>(source => { });
            hostBuilder.Services.Configure<ServiceControlOptions>(hostBuilder.Configuration.GetSection("ServiceControl"));

            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi();

            var app = hostBuilder.Build();
            app.UseServiceControl();
            await app.RunAsync(settings.RootUrl);
        }
    }
}
