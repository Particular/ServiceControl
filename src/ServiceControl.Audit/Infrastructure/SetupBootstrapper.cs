namespace ServiceControl.Audit.Infrastructure
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Auditing.BodyStorage.RavenAttachments;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using Raven.Client;
    using Raven.Client.Embedded;
    using RavenDB;
    using LicenseManagement;
    using Microsoft.Extensions.DependencyInjection;
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
            }

            var configuration = new EndpointConfiguration(settings.ServiceName);
            var assemblyScanner = configuration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            if (excludeAssemblies != null)
            {
                assemblyScanner.ExcludeAssemblies(excludeAssemblies);
            }

            configuration.EnableInstallers(username);

            await new EventSource().Create().ConfigureAwait(false);

            if (settings.SkipQueueCreation)
            {
                log.Info("Skipping queue creation");
                configuration.DoNotCreateQueues();
            }

            var loggingSettings = new LoggingSettings(settings.ServiceName);

            using (var documentStore = new EmbeddableDocumentStore())
            {
                RavenBootstrapper.Configure(documentStore, settings);

                // externally managed container mode doesn't run the installers!
                var containerSettings = configuration.UseContainer(new DefaultServiceProviderFactory());
                var containerBuilder = containerSettings.ServiceCollection;
                containerBuilder.AddSingleton(transportSettings);
                containerBuilder.AddSingleton(loggingSettings);
                containerBuilder.AddSingleton(typeof(IDocumentStore), documentStore);
                containerBuilder.AddSingleton(settings);
                containerBuilder.AddTransient<IDataMigration, MigrateKnownEndpoints>();
                containerBuilder.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();

                NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, loggingSettings,
                    ctx => { }, configuration, false);

                await Endpoint.Create(configuration)
                    .ConfigureAwait(false);


                var ravenOptions = new RavenStartup();
                foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
                {
                    ravenOptions.AddIndexAssembly(indexAssembly);
                }

                var service = new EmbeddedRavenDbHostedService(documentStore, ravenOptions, new IDataMigration[0]);
                await service.SetupDatabase().ConfigureAwait(false);
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