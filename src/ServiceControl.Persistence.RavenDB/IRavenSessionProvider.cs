#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using Raven.Client.Documents.Session;

    public interface IRavenSessionProvider
    {
        IAsyncDocumentSession OpenSession(SessionOptions? options = default);
    }
}