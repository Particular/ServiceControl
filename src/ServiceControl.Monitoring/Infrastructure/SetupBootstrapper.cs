namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using LicenseManagement;
    using Microsoft.Extensions.DependencyInjection;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Monitoring.Settings settings, string[] excludeAssemblies = null)
        {
            this.excludeAssemblies = excludeAssemblies;
            this.settings = settings;
        }

        public Task Run()
        {
            if (ValidateLicense(settings))
            {
                var endpointConfig = new EndpointConfiguration(settings.EndpointName);
                var _ = endpointConfig.UseContainer(new DefaultServiceProviderFactory());

                var bootstrapper = new Bootstrapper(
                    c => Environment.FailFast("NServiceBus Critical Error", c.Exception),
                    settings,
                    endpointConfig);

                var assemblyScanner = endpointConfig.AssemblyScanner();
                if (excludeAssemblies != null)
                {
                    assemblyScanner.ExcludeAssemblies(excludeAssemblies);
                }

                bootstrapper.ConfigureEndpoint(endpointConfig);

                if (settings.SkipQueueCreation)
                {
                    Logger.Info("Skipping queue creation");
                    endpointConfig.DoNotCreateQueues();
                }

                return Endpoint.Create(endpointConfig);
            }

            return Task.CompletedTask;
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
        string[] excludeAssemblies;
    }
}