namespace ServiceControl.Persistence.RavenDb5
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using ServiceControl.Persistence;

    class RavenDbExternalPersistenceLifecycle : IPersistenceLifecycle
    {
        public DocumentStoreProvider StoreProvider { get; }

        public RavenDbExternalPersistenceLifecycle(RavenDBPersisterSettings settings)
        {
            this.settings = settings;

            StoreProvider = new DocumentStoreProvider(() =>
            {
                if (documentStore == null)
                {
                    throw new InvalidOperationException(
                        "Document store is not available until the persistence have been started");
                }

                return documentStore;
            });
        }

        public async Task Start(CancellationToken cancellationToken)
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

            //TODO: copied from Audit, not sure if needed (never assigned). Check and remove
            //if (settings.FindClrType != null)
            //{
            //    store.Conventions.FindClrType += settings.FindClrType;
            //}

            store.Initialize();

            documentStore = store;

            var databaseSetup = new DatabaseSetup(settings);
            await databaseSetup.Execute(store, cancellationToken).ConfigureAwait(false);
        }

        public Task Stop(CancellationToken cancellationToken)
        {
            documentStore?.Dispose();

            return Task.CompletedTask;
        }

        IDocumentStore documentStore;

        readonly RavenDBPersisterSettings settings;
    }
}