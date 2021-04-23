namespace ServiceControl.Alerting.Mail
{
    using System.Threading.Tasks;
    using Alerting;
    using Contracts.CustomChecks;
    using Infrastructure.DomainEvents;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    class CustomChecksMailNotification : IDomainHandler<CustomCheckFailed>, IDomainHandler<CustomCheckSucceeded>
    {
        readonly IDocumentStore store;
        readonly string emailDropFolder;
        readonly string instanceName;

        public CustomChecksMailNotification(IDocumentStore store, Settings settings)
        {
            this.store = store;

            emailDropFolder = settings.EmailDropFolder;
            instanceName = settings.ServiceName;
        }

        public async Task Handle(CustomCheckFailed domainEvent)
        {
            var notificationsSettings = await LoadSettings().ConfigureAwait(false);
            if (notificationsSettings == null || !notificationsSettings.Email.Enabled)
            {
                return;
            }

            var subject = $"[{instanceName}] Health check failed";
            var body = $"Health check {domainEvent.Category}: {domainEvent.CustomCheckId} failed at {domainEvent.FailedAt}. Failure reason {domainEvent.FailureReason}";

            await EmailSender.Send(notificationsSettings.Email, subject, body, emailDropFolder).ConfigureAwait(false);
        }

        public async Task Handle(CustomCheckSucceeded domainEvent)
        {
            var notificationsSettings = await LoadSettings().ConfigureAwait(false);
            if (notificationsSettings == null || !notificationsSettings.Email.Enabled)
            {
                return;
            }

            var subject = $"[{instanceName}] Health check succeeded";
            var body = $"Health check {domainEvent.Category}: {domainEvent.CustomCheckId} succeeded at {domainEvent.SucceededAt}.";

            await EmailSender.Send(notificationsSettings.Email, subject, body, emailDropFolder).ConfigureAwait(false);
        }

        async Task<NotificationsSettings> LoadSettings()
        {
            using (var session = store.OpenAsyncSession())
            {
                return await session.LoadAsync<NotificationsSettings>(NotificationsSettings.SingleDocumentId).ConfigureAwait(false);
            }
        }
    }
}
