namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl;

    sealed class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var hostBuilder = WebApplication.CreateBuilder();
            var settings = hostBuilder.Configuration.Get<Settings>();

            hostBuilder.SetupApplicationConfiguration();
            hostBuilder.Services.Configure<PrimaryOptions>(s => s.RunCleanupBundle = true);

            var endpointConfiguration = new EndpointConfiguration(settings.ServiceControl.InstanceName);

            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi();

            var app = hostBuilder.Build();
            app.UseServiceControl();
            await app.RunAsync(settings.ServiceControl.RootUrl);
        }
    }
}