namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Raven.Client.Documents.Session;

    class RavenDbSessionProvider : IRavenDbSessionProvider
    {
        public RavenDbSessionProvider(IRavenDbDocumentStoreProvider documentStoreProvider)
        {
            this.documentStoreProvider = documentStoreProvider;
        }

        public IAsyncDocumentSession OpenSession()
        {
            return documentStoreProvider.GetDocumentStore()
                .OpenAsyncSession();
        }

        readonly IRavenDbDocumentStoreProvider documentStoreProvider;
    }
}