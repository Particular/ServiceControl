namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents.Session;

    abstract class AbstractSessionManager(IAsyncDocumentSession session) : IDataSessionManager
    {
        protected IAsyncDocumentSession Session { get; } = session;

        public Task SaveChanges(CancellationToken cancellationToken = default) => Session.SaveChangesAsync(cancellationToken);
        public ValueTask DisposeAsync()
        {
            Session.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}