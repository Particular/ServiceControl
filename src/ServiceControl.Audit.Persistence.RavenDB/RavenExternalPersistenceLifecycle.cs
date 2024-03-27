#nullable enable

namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;

    sealed class RavenExternalPersistenceLifecycle(DatabaseConfiguration configuration) : IRavenPersistenceLifecycle, IRavenDocumentStoreProvider, IDisposable
    {
        public IDocumentStore GetDocumentStore()
        {
            if (documentStore == null)
            {
                throw new InvalidOperationException("Document store is not available until the persistence have been started");
            }

            return documentStore;
        }

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            var store = new DocumentStore
            {
                Database = configuration.Name,
                Urls = [configuration.ServerConfiguration.ConnectionString],
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

            var databaseSetup = new DatabaseSetup(configuration);
            await databaseSetup.Execute(store, cancellationToken);
        }

        public void Dispose() => documentStore?.Dispose();

        IDocumentStore? documentStore;
    }
}