namespace ServiceControl.Monitoring.Infrastructure
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LicenseManagement;
    using NServiceBus.Logging;
    using ServiceControl.Transports;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Monitoring.Settings settings)
        {
            this.settings = settings;
        }

        public Task Run()
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

            return transportCustomization.ProvisionQueues(
                settings.Username,
                transportSettings,
                new List<string>());
        }

        bool ValidateLicense(Monitoring.Settings settings)
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

        readonly Monitoring.Settings settings;

        static ILog Logger = LogManager.GetLogger<SetupBootstrapper>();
    }
}