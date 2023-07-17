namespace ServiceControl.Persistence.RavenDb.Editing
{
    using System;
    using System.Threading.Tasks;
    using Notifications;
    using Raven.Client;

    class NotificationsManager : AbstractSessionManager, INotificationsManager
    {
        public NotificationsManager(IAsyncDocumentSession session) : base(session)
        {
        }

        public async Task<NotificationsSettings> LoadSettings(TimeSpan? cacheTimeout = null)
        {
            using (Session.Advanced.DocumentStore.AggressivelyCacheFor(cacheTimeout ?? TimeSpan.Zero))
            {
                var settings = await Session
                    .LoadAsync<NotificationsSettings>(NotificationsSettings.SingleDocumentId)
                    .ConfigureAwait(false);

                if (settings == null)
                {
                    settings = new NotificationsSettings
                    {
                        Id = NotificationsSettings.SingleDocumentId
                    };

                    await Session.StoreAsync(settings).ConfigureAwait(false);
                }

                return settings;
            }
        }
    }
}