namespace Particular.ServiceControl
{
    using System;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure.RavenDB;
    using global::ServiceControl.LicenseManagement;
    using global::ServiceControl.Monitoring;
    using global::ServiceControl.Transports;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings settings, string[] excludedAssemblies = null)
        {
            this.settings = settings;
            this.excludedAssemblies = excludedAssemblies ?? Array.Empty<string>();
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

            if (!settings.RunInMemory) //RunInMemory is used in acceptance tests
            {
                using (var documentStore = new EmbeddableDocumentStore())
                {
                    RavenBootstrapper.Configure(documentStore, settings);
                    var service = new EmbeddedRavenDbHostedService(documentStore, new IDataMigration[0], componentSetupContext);
                    await service.SetupDatabase().ConfigureAwait(false);
                }
            }
            EventSourceCreator.Create();

            if (settings.DataStoreType == DataStoreType.SqlDb)
            {
                await SqlDbMonitoringDataStore.Setup(settings.SqlStorageConnectionString).ConfigureAwait(false);
            }

            if (settings.SkipQueueCreation)
            {
                log.Info("Skipping queue creation");
            }
            else
            {
                var transportSettings = MapSettings(settings);
                var transportCustomization = settings.LoadTransportCustomization();

                var endpointConfig = new EndpointConfiguration(settings.ServiceName);
                endpointConfig.AssemblyScanner().ExcludeAssemblies(excludedAssemblies);

                NServiceBusFactory.Configure(settings, transportCustomization, transportSettings,
                    new LoggingSettings(settings.ServiceName), endpointConfig);

                endpointConfig.EnableInstallers(username);
                var queueBindings = endpointConfig.GetSettings().Get<QueueBindings>();
                foreach (var componentBinding in componentSetupContext.Queues)
                {
                    queueBindings.BindSending(componentBinding);
                }

                // HACK: Do not need the raven persistence to create queues
                endpointConfig.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>();

                await Endpoint.Create(endpointConfig)
                    .ConfigureAwait(false);
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
        readonly string[] excludedAssemblies;
        static ILog log = LogManager.GetLogger<SetupBootstrapper>();
    }
}