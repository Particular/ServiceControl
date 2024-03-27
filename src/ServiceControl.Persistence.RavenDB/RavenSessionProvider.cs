#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using Raven.Client.Documents.Session;

    class RavenSessionProvider(IRavenDocumentStoreProvider documentStoreProvider) : IRavenSessionProvider
    {
        public IAsyncDocumentSession OpenSession(SessionOptions? options = default) =>
            documentStoreProvider.GetDocumentStore()
                .OpenAsyncSession(options ?? new SessionOptions());
    }
}