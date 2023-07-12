namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Raven.Client;

    abstract class AbstractSessionManager : IDataSessionManager
    {
        public IAsyncDocumentSession Session { get; }

        public AbstractSessionManager(IAsyncDocumentSession session)
        {
            Session = session;
        }

        public Task SaveChanges() => Session.SaveChangesAsync();
        public void Dispose() => Session.Dispose();
    }
}
