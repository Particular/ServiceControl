namespace ServiceControl.Persistence.RavenDb5
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Raven.Client.Documents;
    using ServiceControl.Persistence;

    class RavenDbEmbeddedPersistenceLifecycle : IPersistenceLifecycle
    {
        public RavenDbEmbeddedPersistenceLifecycle(RavenDBPersisterSettings databaseConfiguration)
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

        public async Task Start(CancellationToken cancellationToken)
        {
            database = EmbeddedDatabase.Start(databaseConfiguration);

            documentStore = await database.Connect(cancellationToken);
        }

        public Task Stop(CancellationToken cancellationToken)
        {
            documentStore?.Dispose();
            database?.Dispose();

            return Task.CompletedTask;
        }

        IDocumentStore documentStore;
        EmbeddedDatabase database;

        readonly RavenDBPersisterSettings databaseConfiguration;
    }
}