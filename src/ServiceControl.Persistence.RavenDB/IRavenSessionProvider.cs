#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents.Session;

    public interface IRavenSessionProvider
    {
        ValueTask<IAsyncDocumentSession> OpenSession(SessionOptions? options = default, CancellationToken cancellationToken = default);
    }
}