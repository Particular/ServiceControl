namespace Particular.ServiceControl
{
    using Autofac;
    using DbMigrations;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.RavenDB;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    public class DatabaseMigrationsBootstrapper
    {
        public void Run(Settings settings)
        {
            var containerBuilder = new ContainerBuilder();

            var loggingSettings = new LoggingSettings(settings.ServiceName);
            containerBuilder.RegisterInstance(loggingSettings);

            var documentStore = new EmbeddableDocumentStore();
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();
            containerBuilder.RegisterInstance(settings);
            containerBuilder.RegisterModule<MigrationsModule>();

            var markerFileService = new MarkerFileService(loggingSettings.LogPath);

            using (documentStore)
            using (var container = containerBuilder.Build())
            {
                new RavenBootstrapper().StartRaven(documentStore, settings, markerFileService, true);
                container.Resolve<MigrationsManager>().ApplyMigrations();
            }
        }
    }
}