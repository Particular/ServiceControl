namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using ServiceControl.Infrastructure.RavenDB;
    using Raven.Client.Embedded;

    class RavenDbInstaller : IPersistenceInstaller
    {
        public RavenDbInstaller(EmbeddableDocumentStore documentStore, RavenStartup ravenStartup)
        {
            this.documentStore = documentStore;
            this.ravenStartup = ravenStartup;
        }

        public async Task Install(CancellationToken cancellationToken = default)
        {
            Logger.Info("Database initialization starting");
            documentStore.Initialize();
            Logger.Info("Database initialization complete");

            Logger.Info("Index creation started");
            await ravenStartup.CreateIndexesAsync(documentStore);
            Logger.Info("Index creation complete");

            Logger.Info("Data migrations starting");
            var purgeTempIdKnownEndpoints = new PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicateDataMigration();
            await purgeTempIdKnownEndpoints.Migrate(documentStore);
            Logger.Info("Data migrations complete");

        }

        readonly EmbeddableDocumentStore documentStore;
        readonly RavenStartup ravenStartup;

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenDbPersistenceLifecycle));

    }
}
