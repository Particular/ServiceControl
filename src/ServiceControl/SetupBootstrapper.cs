namespace Particular.ServiceControl
{
    using Autofac;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public class SetupBootstrapper
    {
        private readonly Settings settings;

        public SetupBootstrapper(Settings settings)
        {
            this.settings = settings;
        }

        public void Run(string username)
        {
            var configuration = new BusConfiguration();
            configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
            configuration.EnableInstallers(username);

            if (settings.SkipQueueCreation)
            {
                log.Info("Skipping queue creation");
                configuration.DoNotCreateQueues();
            }

            var containerBuilder = new ContainerBuilder();

            var domainEvents = new DomainEvents();
            containerBuilder.RegisterInstance(domainEvents).As<IDomainEvents>();
            var loggingSettings = new LoggingSettings(settings.ServiceName);
            containerBuilder.RegisterInstance(loggingSettings);
            var documentStore = new EmbeddableDocumentStore();
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();
            containerBuilder.RegisterInstance(settings);

            using (documentStore)
            using (var container = containerBuilder.Build())
            using (NServiceBusFactory.Create(settings, container, null, documentStore, configuration, false))
            {
            }
        }

        private static ILog log = LogManager.GetLogger<SetupBootstrapper>();
    }
}