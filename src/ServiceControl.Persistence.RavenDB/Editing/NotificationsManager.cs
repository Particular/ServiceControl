namespace ServiceControl.Persistence.RavenDB.Editing
{
    using System.Threading.Tasks;
    using Notifications;
    using Raven.Client.Documents.Session;

    class NotificationsManager(IAsyncDocumentSession session) : AbstractSessionManager(session), INotificationsManager
    {
        const string SingleDocumentId = "NotificationsSettings/All";

        public async Task<NotificationsSettings> LoadSettings()
        {
            // Deliberately not aggressively cached. These settings are read rarely and edited by hand,
            // and aggressive caching invalidates asynchronously via the Changes API, so a read straight
            // after a save can return the pre-save document.
            var settings = await Session
                .LoadAsync<NotificationsSettingsDocument>(SingleDocumentId);

            if (settings == null)
            {
                settings = new NotificationsSettingsDocument { Id = SingleDocumentId };
                await Session.StoreAsync(settings);
            }

            return new NotificationsSettings()
            {
                Email = settings.Email
            };
        }
    }
}