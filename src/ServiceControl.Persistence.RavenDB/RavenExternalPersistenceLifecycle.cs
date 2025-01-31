#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using ServiceControl.RavenDB;

    sealed class RavenExternalPersistenceLifecycle(RavenPersisterSettings settings) : IRavenPersistenceLifecycle, IRavenDocumentStoreProvider, IDisposable
    {
        public async ValueTask<IDocumentStore> GetDocumentStore(CancellationToken cancellationToken = default)
        {
            if (documentStore != null)
            {
                return documentStore;
            }

            try
            {
                await initializeSemaphore.WaitAsync(cancellationToken);
                return documentStore ?? throw new InvalidOperationException("Document store is not available. Ensure `IRavenPersistenceLifecycle.Initialize` is invoked");
            }
            finally
            {
                initializeSemaphore.Release();
            }
        }

        public async Task Initialize(CancellationToken cancellationToken)
        {
            try
            {
                await initializeSemaphore.WaitAsync(cancellationToken);

                var store = new DocumentStore
                {
                    Database = settings.DatabaseName,
                    Urls = [settings.ConnectionString],
                    Certificate = RavenClientCertificate.FindClientCertificate(),
                    Conventions = new DocumentConventions
                    {
                        SaveEnumsAsIntegers = true
                    }
                };

                documentStore = store.Initialize();

                await StartupChecks.EnsureServerVersion(store, cancellationToken);

                var databaseSetup = new DatabaseSetup(settings, store);
                await databaseSetup.Execute(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                initializeSemaphore.Release();
            }
        }

        public void Dispose() => documentStore?.Dispose();

        IDocumentStore? documentStore;
        readonly SemaphoreSlim initializeSemaphore = new(1, 1);
    }
}