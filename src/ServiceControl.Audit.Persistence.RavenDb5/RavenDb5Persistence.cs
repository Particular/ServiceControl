namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using UnitOfWork;

    class RavenDb5Persistence : IPersistence
    {
        public RavenDb5Persistence(DatabaseConfiguration databaseConfiguration, DatabaseSetup databaseSetup)
        {
            this.databaseConfiguration = databaseConfiguration;
            this.databaseSetup = databaseSetup;
        }

        public IPersistenceLifecycle Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(databaseConfiguration);
            serviceCollection.AddSingleton<IRavenDbSessionProvider, RavenDbSessionProvider>();
            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();

            var lifecycle = CreateLifecycle();

            serviceCollection.AddSingleton<IRavenDbDocumentStoreProvider>(_ => lifecycle);

            return lifecycle;
        }

        public IPersistenceInstaller CreateInstaller() => new RavenDb5Installer(CreateLifecycle(), databaseSetup);

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
        readonly DatabaseSetup databaseSetup;
    }
}