namespace ServiceControl.Persistence.RavenDB.Editing
{
    using System.Threading;
    using System.Threading.Tasks;

    class EditFailedMessagesDataStore(IRavenSessionProvider sessionProvider, ExpirationManager expirationManager) : IEditFailedMessagesDataStore
    {
        public async Task<IEditFailedMessagesManager> CreateEditFailedMessageManager(CancellationToken cancellationToken = default) =>
            // the edit failed message manager manages the lifetime of the session
            new EditFailedMessageManager(await sessionProvider.OpenSession(cancellationToken: cancellationToken), expirationManager);
    }
}
