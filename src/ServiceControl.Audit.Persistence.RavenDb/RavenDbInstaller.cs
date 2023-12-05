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

            // If the SagaDetailsIndex exists but does not have a .Take(50000), then we remove the current SagaDetailsIndex and
            // create a new one. If we do not remove the current one, then RavenDB will attempt to do a side-by-side migration.
            // Doing a side-by-side migration results in the index never swapping if there is constant ingestion as RavenDB will wait.
            // for the index to not be stale before swapping to the new index. Constant ingestion means the index will never be not-stale.
            // This needs to stay in place on version v4.x.x indefinitely.
            var sagaDetailsIndexDefinition = documentStore.DatabaseCommands.GetIndex("SagaDetailsIndex");
            if (sagaDetailsIndexDefinition != null && !sagaDetailsIndexDefinition.Reduce.Contains("Take(50000)"))
            {
                documentStore.DatabaseCommands.DeleteIndex("SagaDetailsIndex");
            }

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