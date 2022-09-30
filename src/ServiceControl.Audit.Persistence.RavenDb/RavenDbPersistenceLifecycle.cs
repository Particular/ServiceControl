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