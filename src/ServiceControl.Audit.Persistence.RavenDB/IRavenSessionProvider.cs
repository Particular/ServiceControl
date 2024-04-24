#nullable enable

namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents.Session;

    interface IRavenSessionProvider
    {
        ValueTask<IAsyncDocumentSession> OpenSession(SessionOptions? options = default, CancellationToken cancellationToken = default);
    }
}