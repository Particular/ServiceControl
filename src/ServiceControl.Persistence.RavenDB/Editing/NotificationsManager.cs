namespace ServiceControl.Persistence.RavenDB.Editing
{
    using System;
    using System.Threading.Tasks;
    using Notifications;
    using Raven.Client.Documents.Session;

    class NotificationsManager(IAsyncDocumentSession session) : AbstractSessionManager(session), INotificationsManager
    {
        const string SingleDocumentId = "NotificationsSettings/All";
        static readonly TimeSpan CacheTimeout = TimeSpan.FromMinutes(5); // Raven requires this to be at least 1 second

        public async Task<NotificationsSettings> LoadSettings()
        {
            using var aggressivelyCacheFor = await Session.Advanced.DocumentStore.AggressivelyCacheForAsync(CacheTimeout);
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