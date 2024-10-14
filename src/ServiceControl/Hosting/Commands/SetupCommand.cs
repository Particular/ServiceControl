namespace ServiceControl.Hosting.Commands
{
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class SetupCommand : AbstractCommand
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
                Logger.Info("Skipping queue creation");
            }
            else
            {
                var transportSettings = settings.ToTransportSettings();
                var transportCustomization = TransportFactory.Create(transportSettings);

                await transportCustomization.ProvisionQueues(transportSettings, componentSetupContext.Queues);
            }

            await host.StopAsync();
        }

        static readonly ILog Logger = LogManager.GetLogger<SetupCommand>();
    }
}