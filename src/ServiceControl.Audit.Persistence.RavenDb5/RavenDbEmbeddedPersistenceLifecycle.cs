namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.Audit.Persistence.RavenDb5;

    class RavenDbEmbeddedPersistenceLifecycle : IRavenDbPersistenceLifecycle
    {
        public RavenDbEmbeddedPersistenceLifecycle(string dbPath, string databaseMaintenanceUrl, AuditDatabaseConfiguration dataBaseConfiguration)
        {
            this.dbPath = dbPath;
            this.databaseMaintenanceUrl = databaseMaintenanceUrl;
            this.dataBaseConfiguration = dataBaseConfiguration;
        }

        public IDocumentStore GetDocumentStore()
        {
            if (documentStore == null)
            {
                throw new InvalidOperationException("Document store is not available until the persistence have been started");
            }

            return documentStore;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            database = EmbeddedDatabase.Start(dbPath, databaseMaintenanceUrl, dataBaseConfiguration);

            documentStore = await database.Connect(cancellationToken).ConfigureAwait(false);
        }

        public Task Stop(CancellationToken cancellationToken)
        {
            documentStore?.Dispose();
            database?.Dispose();

            return Task.CompletedTask;
        }

        IDocumentStore documentStore;
        EmbeddedDatabase database;

        readonly string dbPath;
        readonly string databaseMaintenanceUrl;
        readonly AuditDatabaseConfiguration dataBaseConfiguration;
    }
}