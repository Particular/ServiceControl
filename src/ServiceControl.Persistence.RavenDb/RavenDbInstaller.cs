namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
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

            await ravenStartup.CreateIndexesAsync(documentStore);

            Logger.Info("Data migrations starting **TODO NOT IMPLEMENTED YET**");

            // TODO: Figure out migrations
            // This was copied from audit code:
            ////var endpointMigrations = new MigrateKnownEndpoints(documentStore);
            ////await endpointMigrations.Migrate(cancellationToken: cancellationToken)
            ////    ;
            // While this was copied from EmbeddedRavenDbHostedService in main ServiceControl project, but list was injected
            ////foreach (var migration in dataMigrations)
            ////{
            ////    await migration.Migrate(documentStore)
            ////        ;
            ////}

            Logger.Info("Data migrations complete");

        }

        readonly EmbeddableDocumentStore documentStore;
        readonly RavenStartup ravenStartup;

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenDbPersistenceLifecycle));

    }
}
