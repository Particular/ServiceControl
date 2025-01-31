#nullable enable

namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using ServiceControl.RavenDB;

    sealed class RavenExternalPersistenceLifecycle(DatabaseConfiguration configuration) : IRavenPersistenceLifecycle, IRavenDocumentStoreProvider, IDisposable
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

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            try
            {
                await initializeSemaphore.WaitAsync(cancellationToken);

                var store = new DocumentStore
                {
                    Database = configuration.Name,
                    Urls = [configuration.ServerConfiguration.ConnectionString],
                    Certificate = RavenClientCertificate.FindClientCertificate(configuration.ServerConfiguration.ClientCertificateBase64),
                    Conventions = new DocumentConventions
                    {
                        SaveEnumsAsIntegers = true
                    }
                };

                if (configuration.FindClrType != null)
                {
                    store.Conventions.FindClrType += configuration.FindClrType;
                }

                documentStore = store.Initialize();

                await StartupChecks.EnsureServerVersion(store, cancellationToken);

                var databaseSetup = new DatabaseSetup(configuration);
                await databaseSetup.Execute(store, cancellationToken);
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