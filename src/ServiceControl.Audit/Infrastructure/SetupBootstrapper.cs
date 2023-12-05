namespace ServiceControl.Audit.Infrastructure
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LicenseManagement;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence;
    using Transports;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings.Settings settings)
        {
            this.settings = settings;
        }

        public async Task Run()
        {
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
                    log.Info("Skipping queue creation");
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

            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings.PersistenceType);
            var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);
            var persistence = persistenceConfiguration.Create(persistenceSettings);
            var installer = persistence.CreateInstaller();

            await installer.Install();
        }

        bool ValidateLicense(Settings.Settings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                if (!LicenseManager.IsLicenseValidForServiceControlInit(settings.LicenseFileText, out var errorMessageForLicenseText))
                {
                    log.Error(errorMessageForLicenseText);
                    return false;
                }

                if (!LicenseManager.TryImportLicenseFromText(settings.LicenseFileText, out var importErrorMessage))
                {
                    log.Error(importErrorMessage);
                    return false;
                }
            }
            else
            {
                var license = LicenseManager.FindLicense();
                if (!LicenseManager.IsLicenseValidForServiceControlInit(license, out var errorMessageForFoundLicense))
                {
                    log.Error(errorMessageForFoundLicense);
                    return false;
                }
            }

            return true;
        }

        static TransportSettings MapSettings(Settings.Settings settings)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = settings.ServiceName,
                ConnectionString = settings.TransportConnectionString,
                MaxConcurrency = settings.MaximumConcurrencyLevel
            };
            return transportSettings;
        }

        readonly Settings.Settings settings;
        static ILog log = LogManager.GetLogger<SetupBootstrapper>();
    }
}