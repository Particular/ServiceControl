#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using ServiceControl.RavenDB;

    sealed class RavenExternalPersistenceLifecycle(RavenPersisterSettings settings, ILogger<RavenExternalPersistenceLifecycle> logger) : IRavenPersistenceLifecycle, IRavenDocumentStoreProvider, IDisposable
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
                    Database = settings.DatabaseName,
                    Urls = [settings.ConnectionString],
                    Certificate = RavenClientCertificate.FindClientCertificate(settings),
                    Conventions = new DocumentConventions
                    {
                        SaveEnumsAsIntegers = true,
                        SaveEnumsAsIntegersForPatching = true
                    }
                };

                documentStore = store.Initialize();

                await StartupChecks.EnsureServerVersion(store, cancellationToken);

                var databaseSetup = new DatabaseSetup(settings, store);
                await DatabaseLoadRetry.Execute(databaseSetup.Execute, logger, cancellationToken);
            }
            finally
            {
                initializeSemaphore.Release();
            }
        }

        public Task Stop(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose() => documentStore?.Dispose();

        IDocumentStore? documentStore;
        readonly SemaphoreSlim initializeSemaphore = new(1, 1);
    }
}