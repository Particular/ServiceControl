namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl;

    sealed class RunCommand(Settings settings) : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.ServiceControl.InstanceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");


            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.SetupApplicationConfiguration();
            hostBuilder.Services.Configure<PrimaryOptions>(s => s.RunCleanupBundle = true);

            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi();

            var app = hostBuilder.Build();
            app.UseServiceControl();
            await app.RunAsync(settings.ServiceControl.RootUrl);
        }
    }
}