namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LicenseManagement;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using Persistence;
    using Settings;
    using Transports;
    using WebApi;

    class SetupCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            settings.SkipQueueCreation = args.SkipQueueCreation;

            // Validate license:
            if (!ValidateLicense(settings))
            {
                return;
            }

            var transportSettings = MapSettings(settings);
            var transportCustomization = settings.LoadTransportCustomization();

            if (settings.IngestAuditMessages)
            {
                if (settings.SkipQueueCreation)
                {
                    Logger.Info("Skipping queue creation");
                }
                else
                {
                    var additionalQueues = new List<string> { settings.AuditQueue };

                    if (settings.ForwardAuditMessages && settings.AuditLogQueue != null)
                    {
                        additionalQueues.Add(settings.AuditLogQueue);
                    }

                    await transportCustomization.ProvisionQueues(transportSettings, additionalQueues);
                }
            }

            EventSource.Create();

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.AddServiceControlAuditInstallers(settings);

            var host = hostBuilder.Build();
            await host.RunAsync();
        }

        bool ValidateLicense(Settings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                if (!LicenseManager.IsLicenseValidForServiceControlInit(settings.LicenseFileText, out var errorMessageForLicenseText))
                {
                    Logger.Error(errorMessageForLicenseText);
                    return false;
                }

                if (!LicenseManager.TryImportLicenseFromText(settings.LicenseFileText, out var importErrorMessage))
                {
                    Logger.Error(importErrorMessage);
                    return false;
                }
            }
            else
            {
                var license = LicenseManager.FindLicense();
                if (!LicenseManager.IsLicenseValidForServiceControlInit(license, out var errorMessageForFoundLicense))
                {
                    Logger.Error(errorMessageForFoundLicense);
                    return false;
                }
            }

            return true;
        }

        static TransportSettings MapSettings(Settings settings)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = settings.ServiceName,
                ConnectionString = settings.TransportConnectionString,
                MaxConcurrency = settings.MaximumConcurrencyLevel
            };
            return transportSettings;
        }

        static readonly ILog Logger = LogManager.GetLogger<SetupCommand>();
    }
}