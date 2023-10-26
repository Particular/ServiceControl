namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using RavenDb5.CustomChecks;
    using UnitOfWork;

    class RavenDBPersistence : IPersistence
    {
        public RavenDBPersistence(DatabaseConfiguration databaseConfiguration)
        {
            this.databaseConfiguration = databaseConfiguration;
        }

        public IPersistenceLifecycle Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(databaseConfiguration);
            serviceCollection.AddSingleton<IRavenDbSessionProvider, RavenDbSessionProvider>();
            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();
            serviceCollection.AddSingleton<CheckMinimumStorageRequiredForAuditIngestion.State>();

            var lifecycle = CreateLifecycle();

            serviceCollection.AddSingleton<IRavenDbDocumentStoreProvider>(_ => lifecycle);

            return lifecycle;
        }

        public IPersistenceInstaller CreateInstaller() => new RavenDBInstaller(CreateLifecycle());

        IRavenDbPersistenceLifecycle CreateLifecycle()
        {
            var serverConfiguration = databaseConfiguration.ServerConfiguration;

            if (serverConfiguration.UseEmbeddedServer)
            {
                return new RavenDbEmbeddedPersistenceLifecycle(databaseConfiguration);
            }

            return new RavenDbExternalPersistenceLifecycle(databaseConfiguration);
        }

        readonly DatabaseConfiguration databaseConfiguration;
    }
}