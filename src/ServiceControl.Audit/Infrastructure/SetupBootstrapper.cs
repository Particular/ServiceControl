namespace ServiceControl.Audit.Infrastructure
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Settings;
    using Transports;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings.Settings settings)
        {
            this.settings = settings;
        }

        public async Task Run(string username)
        {
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

            configuration.EnableInstallers(username);

            if (settings.SkipQueueCreation)
            {
                log.Info("Skipping queue creation");
                configuration.DoNotCreateQueues();
            }

            var containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterInstance(transportSettings).SingleInstance();

            var loggingSettings = new LoggingSettings(settings.ServiceName, false);
            containerBuilder.RegisterInstance(loggingSettings).SingleInstance();
            var documentStore = new EmbeddableDocumentStore();
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();
            containerBuilder.RegisterInstance(settings).SingleInstance();

            using (documentStore)
            using (var container = containerBuilder.Build())
            {
                await NServiceBusFactory.Create(settings, settings.LoadTransportCustomization(), transportSettings, loggingSettings, container, ctx => { },documentStore, configuration, false)
                    .ConfigureAwait(false);
            }
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

        private readonly Settings.Settings settings;

        private static ILog log = LogManager.GetLogger<SetupBootstrapper>();
    }
}