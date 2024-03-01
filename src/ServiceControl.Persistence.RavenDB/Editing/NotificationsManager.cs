namespace ServiceControl.Persistence.RavenDB.Editing
{
    using System;
    using System.Threading.Tasks;
    using Notifications;
    using Raven.Client.Documents.Session;

    class NotificationsManager(IAsyncDocumentSession session) : AbstractSessionManager(session), INotificationsManager
    {
        static readonly TimeSpan CacheTimeoutDefault = TimeSpan.FromMinutes(5); // Raven requires this to be at least 1 second

        public async Task<NotificationsSettings> LoadSettings(TimeSpan? cacheTimeout = null)
        {
            using var aggressivelyCacheFor = await Session.Advanced.DocumentStore.AggressivelyCacheForAsync(cacheTimeout ?? CacheTimeoutDefault);
            var settings = await Session
                .LoadAsync<NotificationsSettings>(NotificationsSettings.SingleDocumentId);

            if (settings != null)
            {
                return settings;
            }

            settings = new NotificationsSettings
            {
                Id = NotificationsSettings.SingleDocumentId
            };

            await Session.StoreAsync(settings);

            return settings;
        }
    }
}