#nullable enable

namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents.Session;

    class RavenSessionProvider(IRavenDocumentStoreProvider documentStoreProvider) : IRavenSessionProvider
    {
        public async ValueTask<IAsyncDocumentSession> OpenSession(SessionOptions? options = default, CancellationToken cancellationToken = default)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            return documentStore.OpenAsyncSession(options ?? new SessionOptions());
        }
    }
}