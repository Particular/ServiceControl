namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceControl.LicenseManagement;

    class SetupCommand : AbstractCommand
    {
        public override Task Execute(Settings settings)
        {
            if (ValidateLicense(settings))
            {
                var endpointConfig = new EndpointConfiguration(settings.EndpointName);

                new Bootstrapper(
                    c => Environment.FailFast("NServiceBus Critical Error", c.Exception),
                    settings,
                    endpointConfig);

                endpointConfig.EnableInstallers(settings.Username);

                if (settings.SkipQueueCreation)
                {
                    endpointConfig.DoNotCreateQueues();
                }

                return Endpoint.Create(endpointConfig);
            }

            return Task.CompletedTask;
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

            // Validate license:
            var license = LicenseManager.FindLicense();
            if (!LicenseManager.IsLicenseValidForServiceControlInit(license, out var errorMessageForFoundLicense))
            {
                Logger.Error(errorMessageForFoundLicense);
                return false;
            }

            return true;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(SetupCommand));
    }
}