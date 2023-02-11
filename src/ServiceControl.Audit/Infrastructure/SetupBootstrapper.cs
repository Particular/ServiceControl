namespace ServiceControl.Audit.Infrastructure
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LicenseManagement;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence;
    using Settings;
    using Transports;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings.Settings settings, string[] excludeAssemblies = null)
        {
            this.excludeAssemblies = excludeAssemblies;
            this.settings = settings;
        }

        public async Task Run(string username)
        {
            // Validate license:
            if (!ValidateLicense(settings))
            {
                return;
            }

            var transportSettings = MapSettings(settings);
            var transportCustomization = settings.LoadTransportCustomization();
            var factory = new RawEndpointFactory(settings, transportSettings, transportCustomization);

            // if audit queue is ("!disable") IngestAuditMessages will be false
            if (settings.IngestAuditMessages)
            {
                var queueIngestorFactory = factory.CreateQueueIngestorFactory();

                if (settings.SkipQueueCreation)
                {
                    log.Info("Skipping queue creation");
                }
                else
                {
                    var additionalQueues = new List<string>
                    {
                        $"{settings.ServiceName}.Errors"
                    };
                    if (settings.ForwardAuditMessages && settings.AuditLogQueue != null)
                    {
                        additionalQueues.Add(settings.AuditLogQueue);
                    }

                    // TODO: Use a fresh raw endpoint to create all the extra queues
                    //config.AutoCreateQueues(additionalQueues.ToArray(), username);

                    //TODO Should we perhaps not have the ingestor create any queues?
                    await queueIngestorFactory.Setup(settings.AuditQueue, username).ConfigureAwait(false);
                }
            }

            var configuration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = configuration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            if (excludeAssemblies != null)
            {
                assemblyScanner.ExcludeAssemblies(excludeAssemblies);
            }

            configuration.EnableInstallers(username);

            EventSource.Create();

            if (settings.SkipQueueCreation)
            {
                log.Info("Skipping queue creation");
                configuration.DoNotCreateQueues();
            }

            var loggingSettings = new LoggingSettings(settings.ServiceName);

            // externally managed container mode doesn't run the installers!
            var providerFactory = new DefaultServiceProviderFactory();
            var containerSettings = configuration.UseContainer(providerFactory);
            var containerBuilder = containerSettings.ServiceCollection;
            containerBuilder.AddSingleton(transportSettings);
            containerBuilder.AddSingleton(loggingSettings);
            containerBuilder.AddSingleton(settings);

            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration();
            var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);
            var persistence = persistenceConfiguration.Create(persistenceSettings);
            var installer = persistence.CreateInstaller();
            await installer.Install()
                .ConfigureAwait(false);

            NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, loggingSettings,
                ctx => { }, configuration, false);

            await Endpoint.Create(configuration)
                .ConfigureAwait(false);
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
        string[] excludeAssemblies;
    }
}