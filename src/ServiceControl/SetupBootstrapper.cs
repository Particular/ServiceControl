using ServiceControl.Infrastructure.RavenDB;
using ServiceControl.SagaAudit;

namespace Particular.ServiceControl
{
    using System.Threading.Tasks;
    using Autofac;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Transports;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings settings, LoggingSettings loggingSettings, EmbeddedDatabase embeddedDatabase, string[] excludeAssemblies = null)
        {
            this.excludeAssemblies = excludeAssemblies;
            this.settings = settings;
            this.loggingSettings = loggingSettings;
            this.embeddedDatabase = embeddedDatabase;
        }

        public async Task Run(string username)
        {
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

            var domainEvents = new DomainEvents();
            containerBuilder.RegisterInstance(domainEvents).As<IDomainEvents>();

            var transportSettings = MapSettings(settings);
            containerBuilder.RegisterInstance(transportSettings).SingleInstance();

            containerBuilder.RegisterInstance(loggingSettings).SingleInstance();
            var documentStore = await embeddedDatabase.PrepareDatabase("servicecontrol",typeof(SetupBootstrapper).Assembly, typeof(SagaInfo).Assembly).ConfigureAwait(false);
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();
            containerBuilder.RegisterInstance(settings).SingleInstance();
            containerBuilder.RegisterAssemblyTypes(GetType().Assembly).AssignableTo<IAbstractIndexCreationTask>().As<IAbstractIndexCreationTask>();

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
        readonly LoggingSettings loggingSettings;
        readonly EmbeddedDatabase embeddedDatabase;

        private static ILog log = LogManager.GetLogger<SetupBootstrapper>();
        string[] excludeAssemblies;
    }
}