namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Raven.Client.Documents.Session;

    class RavenSessionProvider : IRavenSessionProvider
    {
        public RavenSessionProvider(IRavenDocumentStoreProvider documentStoreProvider)
        {
            this.documentStoreProvider = documentStoreProvider;
        }

        public IAsyncDocumentSession OpenSession()
        {
            return documentStoreProvider.GetDocumentStore()
                .OpenAsyncSession();
        }

        readonly IRavenDocumentStoreProvider documentStoreProvider;
    }
}