namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using LicenseManagement;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using Settings;
    using Transports;

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

        static readonly ILog Logger = LogManager.GetLogger<SetupCommand>();
    }
}