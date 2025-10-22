namespace ServiceControl.Hosting.Commands
{
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;
    using Transports;

    sealed class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.SetupApplicationConfiguration();
            var settings = hostBuilder.Configuration.Get<Settings>();

            hostBuilder.AddServiceControlInstallers(settings);

            var componentSetupContext = new ComponentInstallationContext();

            foreach (ServiceControlComponent component in ServiceControlMainInstance.Components)
            {
                component.Setup(settings, componentSetupContext, hostBuilder);
            }

            using IHost host = hostBuilder.Build();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                EventSourceCreator.Create();
            }

            await host.StartAsync();

            if (args.SkipQueueCreation)
            {
                LoggerUtil.CreateStaticLogger<SetupCommand>().LogInformation("Skipping queue creation");
            }
            else
            {
                var transportSettings = settings.ServiceControl.ToTransportSettings();
                transportSettings.RunCustomChecks = false;
                var transportCustomization = TransportFactory.Create(transportSettings);

                await transportCustomization.ProvisionQueues(transportSettings, componentSetupContext.Queues);
            }

            await host.StopAsync();
        }
    }
}