using ServiceControl.Infrastructure.RavenDB;
using ServiceControl.SagaAudit;

namespace ServiceControl.Audit.Infrastructure
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Embedded;
    using ServiceControl.LicenseManagement;
    using Settings;
    using Transports;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings.Settings settings, LoggingSettings loggingSettings, EmbeddedDatabase embeddedDatabase, string[] excludeAssemblies = null)
        {
            this.excludeAssemblies = excludeAssemblies;
            this.settings = settings;
            this.loggingSettings = loggingSettings;
            this.embeddedDatabase = embeddedDatabase;
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

            var config = factory.CreateAuditIngestor(settings.AuditQueue, (context, dispatcher) => Task.CompletedTask);

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
                config.AutoCreateQueues(additionalQueues.ToArray(), username);
            }

            //No need to start the raw endpoint to create queues
            await RawEndpoint.Create(config).ConfigureAwait(false);

            var configuration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = configuration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            if (excludeAssemblies != null)
            {
                assemblyScanner.ExcludeAssemblies(excludeAssemblies);
            }

            configuration.EnableInstallers(username);

            if (settings.SkipQueueCreation)
            {
                log.Info("Skipping queue creation");
                configuration.DoNotCreateQueues();
            }

            var containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterInstance(transportSettings).SingleInstance();
            containerBuilder.RegisterInstance(loggingSettings).SingleInstance();

            var documentStore = await embeddedDatabase.PrepareDatabase("audit", typeof(SetupBootstrapper).Assembly, typeof(SagaInfo).Assembly).ConfigureAwait(false);
            containerBuilder.Register(c => documentStore).ExternallyOwned();
            containerBuilder.RegisterInstance(settings).SingleInstance();
            containerBuilder.RegisterAssemblyTypes(GetType().Assembly).AssignableTo<IAbstractIndexCreationTask>().As<IAbstractIndexCreationTask>();
            //containerBuilder.RegisterType<MigrateKnownEndpoints>().As<INeedToInstallSomething>();
            using (var container = containerBuilder.Build())
            {
                await NServiceBusFactory.Create(settings, transportCustomization, transportSettings, loggingSettings, container, ctx => { }, documentStore, configuration, false)
                    .ConfigureAwait(false);
            }
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

                if (LicenseManager.TryImportLicenseFromText(settings.LicenseFileText, out var importErrorMessage))
                {
                    log.Error(importErrorMessage);
                    return false;
                }
            }

            // Validate license:
            var license = LicenseManager.FindLicense();
            if (!LicenseManager.IsLicenseValidForServiceControlInit(license, out var errorMessageForFoundLicense))
            {
                log.Error(errorMessageForFoundLicense);
                return false;
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
        readonly LoggingSettings loggingSettings;
        readonly EmbeddedDatabase embeddedDatabase;
        readonly string[] excludeAssemblies;

        static ILog log = LogManager.GetLogger<SetupBootstrapper>();
    }
}