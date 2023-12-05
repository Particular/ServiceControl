namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Audit.Persistence.RavenDB;

    class RavenDbPersistenceLifecycle : IPersistenceLifecycle
    {
        public RavenDbPersistenceLifecycle(RavenStartup ravenStartup, EmbeddableDocumentStore documentStore)
        {
            this.ravenStartup = ravenStartup;
            this.documentStore = documentStore;
        }

        public async Task Start(CancellationToken cancellationToken)
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
        }

        public Task Stop(CancellationToken cancellationToken)
        {
            documentStore.Dispose();
            return Task.CompletedTask;
        }

        readonly RavenStartup ravenStartup;
        readonly EmbeddableDocumentStore documentStore;

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenDbPersistenceLifecycle));
    }
}