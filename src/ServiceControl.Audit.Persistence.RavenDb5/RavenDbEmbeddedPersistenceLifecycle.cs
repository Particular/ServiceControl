namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;

    class RavenDbEmbeddedPersistenceLifecycle : IRavenDbPersistenceLifecycle
    {
        public RavenDbEmbeddedPersistenceLifecycle(DatabaseConfiguration databaseConfiguration)
        {
            this.databaseConfiguration = databaseConfiguration;
        }

        public IDocumentStore GetDocumentStore()
        {
            if (documentStore == null)
            {
                throw new InvalidOperationException("Document store is not available until the persistence have been started");
            }

            return documentStore;
        }

        public async Task Start(Action onRavenServerExit, CancellationToken cancellationToken)
        {
            database = EmbeddedDatabase.Start(databaseConfiguration, onRavenServerExit);

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

        readonly DatabaseConfiguration databaseConfiguration;
    }
}