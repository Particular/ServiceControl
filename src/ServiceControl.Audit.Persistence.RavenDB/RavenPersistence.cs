namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using RavenDB.CustomChecks;
    using UnitOfWork;

    class RavenPersistence : IPersistence
    {
        public RavenPersistence(DatabaseConfiguration databaseConfiguration)
        {
            this.databaseConfiguration = databaseConfiguration;
        }

        public IPersistenceLifecycle Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(databaseConfiguration);
            serviceCollection.AddSingleton<IRavenSessionProvider, RavenSessionProvider>();
            serviceCollection.AddSingleton<IAuditDataStore, RavenAuditDataStore>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenFailedAuditStorage>();
            serviceCollection.AddSingleton<CheckMinimumStorageRequiredForAuditIngestion.State>();

            var lifecycle = CreateLifecycle();

            serviceCollection.AddSingleton<IRavenDocumentStoreProvider>(_ => lifecycle);

            return lifecycle;
        }

        public IPersistenceInstaller CreateInstaller() => new RavenInstaller(CreateLifecycle());

        IRavenPersistenceLifecycle CreateLifecycle()
        {
            var serverConfiguration = databaseConfiguration.ServerConfiguration;

            if (serverConfiguration.UseEmbeddedServer)
            {
                return new RavenEmbeddedPersistenceLifecycle(databaseConfiguration);
            }

            return new RavenExternalPersistenceLifecycle(databaseConfiguration);
        }

        readonly DatabaseConfiguration databaseConfiguration;
    }
}