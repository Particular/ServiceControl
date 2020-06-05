namespace Particular.ServiceControl
{
    using System.Threading.Tasks;
    using Autofac;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Transports;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings settings)
        {
            this.settings = settings;
        }

        public async Task Run(string username)
        {
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

            var domainEvents = new DomainEvents();
            containerBuilder.RegisterInstance(domainEvents).As<IDomainEvents>();

            var transportSettings = MapSettings(settings);
            containerBuilder.RegisterInstance(transportSettings).SingleInstance();

            var loggingSettings = new LoggingSettings(settings.ServiceName);
            containerBuilder.RegisterInstance(loggingSettings).SingleInstance();
            var documentStore = new EmbeddableDocumentStore();
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();
            containerBuilder.RegisterInstance(settings).SingleInstance();

            using (documentStore)
            using (var container = containerBuilder.Build())
            {
                await NServiceBusFactory.Create(settings, settings.LoadTransportCustomization(), transportSettings, loggingSettings, container, documentStore, configuration, false)
                    .ConfigureAwait(false);
            }
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

        private readonly Settings settings;

        private static ILog log = LogManager.GetLogger<SetupBootstrapper>();
    }
}