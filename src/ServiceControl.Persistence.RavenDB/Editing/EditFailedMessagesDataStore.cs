namespace ServiceControl.Persistence.RavenDB.Editing
{
    using System.Threading.Tasks;

    class EditFailedMessagesDataStore(IRavenSessionProvider sessionProvider, ExpirationManager expirationManager) : IEditFailedMessagesDataStore
    {
        public async Task<IEditFailedMessagesManager> CreateEditFailedMessageManager() =>
            // the edit failed message manager manages the lifetime of the session
            new EditFailedMessageManager(await sessionProvider.OpenSession(), expirationManager);
    }
}
