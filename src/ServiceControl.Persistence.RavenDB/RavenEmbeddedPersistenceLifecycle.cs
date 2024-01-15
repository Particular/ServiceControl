namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Exceptions.Database;
    using ServiceControl.Persistence;

    class RavenEmbeddedPersistenceLifecycle : IPersistenceLifecycle, IDisposable
    {
        public RavenEmbeddedPersistenceLifecycle(RavenPersisterSettings databaseConfiguration)
        {
            this.databaseConfiguration = databaseConfiguration;
        }

        public IDocumentStore GetDocumentStore()
        {
            if (database == null)
            {
                throw new InvalidOperationException("Document store is not available. Ensure `IPersistenceLifecycle.Initialize` is invoked");
            }
            if (documentStore != null)
            {
                return documentStore;
            }
            lock (this)
            {
                if (documentStore == null)
                {
                    CreateDocumentStore(CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            return documentStore;
        }

        public async Task Initialize(CancellationToken cancellationToken)
        {
            database = EmbeddedDatabase.Start(databaseConfiguration);
            await CreateDocumentStore(cancellationToken);
        }

        async Task CreateDocumentStore(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Can take over 30 seconds after which a timeout is raised
                    documentStore = await database.Connect(cancellationToken);
                    return;
                }
                catch (DatabaseLoadTimeoutException e)
                {
                    Log.Warn("Could not connect to database. Retrying in 500ms...", e);
                    await Task.Delay(500, cancellationToken);
                }
            }
        }

        public void Dispose()
        {
            documentStore?.Dispose();
            database?.Dispose();
            GC.SuppressFinalize(this);
        }

        IDocumentStore documentStore;
        EmbeddedDatabase database;

        readonly RavenPersisterSettings databaseConfiguration;
        static readonly ILog Log = LogManager.GetLogger(typeof(RavenEmbeddedPersistenceLifecycle));

        ~RavenEmbeddedPersistenceLifecycle()
        {
            Trace.WriteLine("ERROR: RavenDbEmbeddedPersistenceLifecycle isn't properly disposed");
        }
    }
}