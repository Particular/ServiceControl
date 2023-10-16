namespace ServiceControl.Persistence.RavenDb5
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using ServiceControl.Persistence;

    class RavenDbExternalPersistenceLifecycle : IPersistenceLifecycle, IDisposable
    {
        public RavenDbExternalPersistenceLifecycle(RavenDBPersisterSettings settings)
        {
            this.settings = settings;
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
            var store = new DocumentStore
            {
                Database = settings.DatabaseName,
                Urls = new[] { settings.ConnectionString },
                Conventions = new DocumentConventions
                {
                    SaveEnumsAsIntegers = true
                }
            };

            store.Initialize();

            documentStore = store;

            var databaseSetup = new DatabaseSetup(settings);
            await databaseSetup.Execute(store, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            documentStore?.Dispose();
        }

        IDocumentStore documentStore;

        readonly RavenDBPersisterSettings settings;
    }
}