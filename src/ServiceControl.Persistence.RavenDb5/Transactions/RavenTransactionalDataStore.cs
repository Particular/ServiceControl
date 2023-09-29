namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Raven.Client.Documents.Session;

    abstract class AbstractSessionManager : IDataSessionManager
    {
        protected IAsyncDocumentSession Session { get; }

        protected AbstractSessionManager(IAsyncDocumentSession session) => Session = session;
        public Task SaveChanges() => Session.SaveChangesAsync();
        public void Dispose() => Session.Dispose();
    }
}