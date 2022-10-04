namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using RavenDb5;
    using UnitOfWork;

    class RavenDb5Persistence : IPersistence
    {
        public RavenDb5Persistence(IRavenDbPersistenceLifecycle lifecycle, DatabaseSetup databaseSetup, PersistenceSettings settings)
        {
            this.lifecycle = lifecycle;
            this.databaseSetup = databaseSetup;
            this.settings = settings;
        }

        public IPersistenceLifecycle CreateLifecycle(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IRavenDbSessionProvider, RavenDbSessionProvider>();
            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();
            serviceCollection.AddSingleton<IRavenDbDocumentStoreProvider>(_ => lifecycle);

            return lifecycle;
        }

        public IPersistenceInstaller CreateInstaller() => new RavenDb5Installer(lifecycle, databaseSetup);

        readonly IRavenDbPersistenceLifecycle lifecycle;
        readonly DatabaseSetup databaseSetup;
        readonly PersistenceSettings settings;
    }
}