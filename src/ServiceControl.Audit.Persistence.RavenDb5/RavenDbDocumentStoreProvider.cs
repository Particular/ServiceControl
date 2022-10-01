namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Raven.Client.Documents;

    class RavenDbDocumentStoreProvider : IRavenDbDocumentStoreProvider
    {
        public RavenDbDocumentStoreProvider(RavenDbPersistenceLifecycle persistenceLifecycle)
        {
            this.persistenceLifecycle = persistenceLifecycle;
        }

        public IDocumentStore GetDocumentStore()
        {
            return persistenceLifecycle.DocumentStore;
        }

        readonly RavenDbPersistenceLifecycle persistenceLifecycle;
    }
}