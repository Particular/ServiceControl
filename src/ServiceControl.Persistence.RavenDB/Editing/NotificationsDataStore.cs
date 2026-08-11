namespace ServiceControl.Persistence.RavenDB.Editing
{
    using System.Threading;
    using System.Threading.Tasks;

    class NotificationsDataStore(IRavenSessionProvider sessionProvider) : INotificationsDataStore
    {
        public async Task<INotificationsManager> CreateNotificationsManager(CancellationToken cancellationToken = default) =>
            // the notifications manager manages the lifetime of the session
            new NotificationsManager(await sessionProvider.OpenSession(cancellationToken: cancellationToken));
    }
}
