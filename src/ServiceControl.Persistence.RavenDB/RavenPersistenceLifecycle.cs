#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;

    sealed class RavenPersistenceLifecycle(RavenPersisterSettings settings, IConnectionStringProvider connectionStringProvider)
        : IRavenPersistenceLifecycle, IRavenDocumentStoreProvider
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
            var store = new DocumentStore
            {
                Database = settings.DatabaseName,
                Urls = [await connectionStringProvider.GetConnectionString()],
                Conventions = new DocumentConventions
                {
                    SaveEnumsAsIntegers = true
                }
            };

            documentStore = store.Initialize();

            var databaseSetup = new DatabaseSetup(settings);
            await databaseSetup.Execute(store, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose() => documentStore?.Dispose();

        IDocumentStore? documentStore;
    }
}