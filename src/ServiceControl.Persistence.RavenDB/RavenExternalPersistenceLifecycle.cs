﻿#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;
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

                // Look for raven-client-certificate.pfx in same directory as application code
                var potentialCertPath = Path.GetFullPath(Path.Combine(Assembly.GetEntryAssembly()!.Location, "..", "raven-client-certificate.pfx"));
                X509Certificate2? certificate = null;
                if (File.Exists(potentialCertPath))
                {
                    certificate = new X509Certificate2(potentialCertPath);
                }

                var store = new DocumentStore
                {
                    Database = settings.DatabaseName,
                    Urls = [settings.ConnectionString],
                    Certificate = certificate,
                    Conventions = new DocumentConventions
                    {
                        SaveEnumsAsIntegers = true
                    }
                };

                documentStore = store.Initialize();

                await StartupChecks.EnsureServerVersion(store, cancellationToken);

                var databaseSetup = new DatabaseSetup(settings, store);
                await databaseSetup.Execute(cancellationToken).ConfigureAwait(false);

                // Must go after the database setup, as database must exist
                StartupChecks.EnsureSingleNodeTopology(store);
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