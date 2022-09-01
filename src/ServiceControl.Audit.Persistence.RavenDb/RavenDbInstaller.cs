namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Threading;
    using System.Threading.Tasks;
    using Audit.Infrastructure.Migration;
    using NServiceBus.Logging;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using RavenDB;

    class RavenDbInstaller : IPersistenceInstaller
    {
        public RavenDbInstaller(EmbeddableDocumentStore documentStore, RavenStartup ravenStartup)
        {
            this.documentStore = documentStore;
            this.ravenStartup = ravenStartup;
        }

        public async Task Install(CancellationToken cancellationToken)
        {
            Logger.Info("Database initialization starting");
            documentStore.Initialize();
            Logger.Info("Database initialization complete");

            Logger.Info("Index creation started");
            var indexProvider = ravenStartup.CreateIndexProvider();
            await IndexCreation.CreateIndexesAsync(indexProvider, documentStore)
                .ConfigureAwait(false);
            Logger.Info("Index creation complete");

            Logger.Info("Data migrations starting");

            var endpointMigrations = new MigrateKnownEndpoints(documentStore);
            await endpointMigrations.Migrate(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            Logger.Info("Data migrations complete");
        }

        readonly EmbeddableDocumentStore documentStore;
        readonly RavenStartup ravenStartup;

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenDbPersistenceLifecycle));
    }
}