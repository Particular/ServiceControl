namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Infrastructure;
    using Settings;
    using Transports;

    class SetupCommand() : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            if (settings.IngestAuditMessages)
            {
                if (args.SkipQueueCreation)
                {
                    LoggerUtil.CreateStaticLogger<SetupCommand>().LogInformation("Skipping queue creation");
                }
                else
                {
                    var additionalQueues = new List<string> { settings.AuditQueue };

                    if (settings.ForwardAuditMessages && settings.AuditLogQueue != null)
                    {
                        additionalQueues.Add(settings.AuditLogQueue);
                    }

                    var transportSettings = settings.ToTransportSettings();
                    transportSettings.RunCustomChecks = false;
                    var transportCustomization = TransportFactory.Create(transportSettings);

                    await transportCustomization.ProvisionQueues(transportSettings, additionalQueues);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                EventSourceCreator.Create();
            }

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.AddServiceControlAuditInstallers(settings);

            using var host = hostBuilder.Build();
            await host.StartAsync();
            await host.StopAsync();
        }
    }
}