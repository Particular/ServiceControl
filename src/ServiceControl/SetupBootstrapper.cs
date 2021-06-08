namespace Particular.ServiceControl
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure.RavenDB;
    using global::ServiceControl.LicenseManagement;
    using global::ServiceControl.Transports;
    using NServiceBus.Logging;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings settings)
        {
            this.settings = settings;
        }

        public async Task Run(string username)
        {
            // Validate license:
            if (!ValidateLicense(settings))
            {
                return;
            }

            var componentSetupContext = new ComponentSetupContext();

            foreach (ServiceControlComponent component in ServiceControlMainInstance.Components)
            {
                component.Setup(settings, componentSetupContext);
            }

            using (var documentStore = new EmbeddableDocumentStore())
            {
                RavenBootstrapper.Configure(documentStore, settings);
                var service = new EmbeddedRavenDbHostedService(documentStore, new IDataMigration[0], componentSetupContext);
                await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
                await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }

            EventSourceCreator.Create();

            if (settings.SkipQueueCreation)
            {
                log.Info("Skipping queue creation");
            }
            else
            {
                var transportSettings = MapSettings(settings);
                var transportCustomization = settings.LoadTransportCustomization();

                await QueueCreator.CreateQueues(transportSettings, transportCustomization.CustomizeServiceControlEndpoint, username, componentSetupContext.Queues.ToArray()).ConfigureAwait(false);
            }
        }

        bool ValidateLicense(Settings settings)
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

        readonly Settings settings;
        static ILog log = LogManager.GetLogger<SetupBootstrapper>();
    }
}