namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using LicenseManagement;
    using NServiceBus.Logging;
    using Transports;

    class SetupCommand : AbstractCommand
    {
        public override Task Execute(Settings settings)
        {
            if (!ValidateLicense(settings))
            {
                return Task.CompletedTask;
            }

            if (settings.SkipQueueCreation)
            {
                Logger.Info("Skipping queue creation");
                return Task.CompletedTask;
            }

            var transportCustomization = settings.LoadTransportCustomization();

            var transportSettings = new TransportSettings
            {
                RunCustomChecks = false,
                ConnectionString = settings.ConnectionString,
                EndpointName = settings.EndpointName,
                ErrorQueue = settings.ErrorQueue,
                MaxConcurrency = settings.MaximumConcurrencyLevel
            };

            return transportCustomization.ProvisionQueues(transportSettings, []);
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

        static ILog Logger = LogManager.GetLogger<SetupCommand>();
    }
}