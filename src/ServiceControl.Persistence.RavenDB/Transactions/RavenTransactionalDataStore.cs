namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading.Tasks;
    using Raven.Client.Documents.Session;

    abstract class AbstractSessionManager(IAsyncDocumentSession session) : IDataSessionManager
    {
        protected IAsyncDocumentSession Session { get; } = session;

        public Task SaveChanges() => Session.SaveChangesAsync();
        public void Dispose() => Session.Dispose();
    }
}