namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;
    using ServiceControl.Persistence;
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

            var logger = LoggerUtil.CreateStaticLogger<SetupCommand>();
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

            // Create message body storage directory if it doesn't exist
            var persistenceSettings = host.Services.GetRequiredService<PersistenceSettings>();
            if (!string.IsNullOrEmpty(persistenceSettings.MessageBodyStoragePath))
            {
                if (!Directory.Exists(persistenceSettings.MessageBodyStoragePath))
                {
                    logger.LogInformation("Creating message body storage directory: {StoragePath}", persistenceSettings.MessageBodyStoragePath);
                    Directory.CreateDirectory(persistenceSettings.MessageBodyStoragePath);
                }
            }
            else
            {
                throw new Exception("Message body storage path is not configured.");
            }

            await host.Services.GetRequiredService<IDatabaseMigrator>().ApplyMigrations();

            await host.StopAsync();
        }
    }
}