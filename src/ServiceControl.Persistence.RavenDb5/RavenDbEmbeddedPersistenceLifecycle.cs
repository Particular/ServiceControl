namespace ServiceControl.Persistence.RavenDb5
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.Persistence;

    class RavenDbEmbeddedPersistenceLifecycle : IPersistenceLifecycle, IDisposable
    {
        public RavenDbEmbeddedPersistenceLifecycle(RavenDBPersisterSettings databaseConfiguration)
        {
            this.databaseConfiguration = databaseConfiguration;
        }

        public IDocumentStore GetDocumentStore()
        {
            if (documentStore == null)
            {
                throw new InvalidOperationException("Document store is not available. Ensure `IPersistenceLifecycle.Initialize` is invoked");
            }

            return documentStore;
        }

        public async Task Initialize(CancellationToken cancellationToken)
        {
            database = EmbeddedDatabase.Start(databaseConfiguration);
            documentStore = await database.Connect(cancellationToken);
        }

        public void Dispose()
        {
            documentStore?.Dispose();
            database?.Dispose();
            GC.SuppressFinalize(this);
        }

        IDocumentStore documentStore;
        EmbeddedDatabase database;

        readonly RavenDBPersisterSettings databaseConfiguration;

        ~RavenDbEmbeddedPersistenceLifecycle()
        {
            Trace.WriteLine("ERROR: RavenDbEmbeddedPersistenceLifecycle isn't properly disposed");
        }
    }
}