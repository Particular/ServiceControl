namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;

    class RavenDbPersistenceLifecycle : IPersistenceLifecycle
    {
        public RavenDbPersistenceLifecycle(EmbeddedDatabase database)
        {
            this.database = database;
        }

        public IDocumentStore DocumentStore
        {
            get
            {
                if (documentStore == null)
                {
                    throw new InvalidOperationException("Document store is not available until the persistence have been started");
                }

                return documentStore;
            }
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            documentStore = await database.Initialize(cancellationToken).ConfigureAwait(false);
        }

        public Task Stop(CancellationToken cancellationToken)
        {
            documentStore?.Dispose();
            database?.Dispose();

            return Task.CompletedTask;
        }

        IDocumentStore documentStore;

        readonly EmbeddedDatabase database;
    }
}