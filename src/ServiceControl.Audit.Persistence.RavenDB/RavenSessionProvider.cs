namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Raven.Client.Documents.Session;

    class RavenSessionProvider(IRavenDocumentStoreProvider documentStoreProvider) : IRavenSessionProvider
    {
        public IAsyncDocumentSession OpenSession() =>
            documentStoreProvider.GetDocumentStore()
                .OpenAsyncSession();
    }
}