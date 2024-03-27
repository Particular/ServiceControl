#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Exceptions.Database;

    sealed class RavenEmbeddedPersistenceLifecycle(RavenPersisterSettings databaseConfiguration, IHostApplicationLifetime lifetime)
        : IRavenPersistenceLifecycle, IRavenDocumentStoreProvider, IDisposable
    {
        public IDocumentStore GetDocumentStore()
        {
            if (documentStore == null)
            {
                throw new InvalidOperationException("Document store is not available. Ensure `IRavenPersistenceLifecycle.Initialize` is invoked");
            }

            return documentStore;
        }

        public async Task Initialize(CancellationToken cancellationToken)
        {
            database = EmbeddedDatabase.Start(databaseConfiguration, lifetime);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
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
        }

        IDocumentStore? documentStore;
        EmbeddedDatabase? database;

        static readonly ILog Log = LogManager.GetLogger(typeof(RavenEmbeddedPersistenceLifecycle));
    }
}