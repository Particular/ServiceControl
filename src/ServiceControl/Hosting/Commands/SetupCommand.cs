namespace ServiceControl.Hosting.Commands
{
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class SetupCommand(ILogger<SetupCommand> logger) : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var hostBuilder = Host.CreateApplicationBuilder();
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
                logger.LogInformation("Skipping queue creation");
            }
            else
            {
                var transportSettings = settings.ToTransportSettings();
                transportSettings.RunCustomChecks = false;
                var transportCustomization = TransportFactory.Create(transportSettings);

                await transportCustomization.ProvisionQueues(transportSettings, componentSetupContext.Queues);
            }

            await host.StopAsync();
        }
    }
}