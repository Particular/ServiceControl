namespace ServiceControl.Persistence.RavenDB.Editing
{
    using System.Threading;
    using System.Threading.Tasks;
    using Notifications;

    class NotificationsDataStore(IRavenSessionProvider sessionProvider) : INotificationsDataStore
    {
        const string SingleDocumentId = "NotificationsSettings/All";

        public async Task<NotificationsSettings> LoadSettings(CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var document = await session.LoadAsync<NotificationsSettingsDocument>(SingleDocumentId, cancellationToken);

            return new NotificationsSettings
            {
                Email = Copy(document?.Email ?? new EmailNotifications())
            };
        }

        public async Task SaveSettings(NotificationsSettings settings, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var document = new NotificationsSettingsDocument
            {
                Id = SingleDocumentId,
                Email = Copy(settings.Email)
            };

            await session.StoreAsync(document, SingleDocumentId, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        static EmailNotifications Copy(EmailNotifications source) => new()
        {
            Enabled = source.Enabled,
            SmtpServer = source.SmtpServer,
            SmtpPort = source.SmtpPort,
            AuthenticationAccount = source.AuthenticationAccount,
            AuthenticationPassword = source.AuthenticationPassword,
            EnableTLS = source.EnableTLS,
            To = source.To,
            From = source.From
        };
    }
}
