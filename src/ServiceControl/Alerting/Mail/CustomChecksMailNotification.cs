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

        public CustomChecksMailNotification(IDocumentStore store, Settings settings)
        {
            this.store = store;
            emailDropFolder = settings.EmailDropFolder;
        }

        public async Task Handle(CustomCheckFailed domainEvent)
        {
            var settings = await LoadSettings().ConfigureAwait(false);
            if (settings == null || !settings.Email.Enabled)
            {
                return;
            }

            await EmailSender.Send(
                    settings.Email,
                "Health check failed",
                $"Health check {domainEvent.Category}: {domainEvent.CustomCheckId} failed at {domainEvent.FailedAt}. Failure reason {domainEvent.FailureReason}",
                    emailDropFolder)
                .ConfigureAwait(false);
        }

        public async Task Handle(CustomCheckSucceeded domainEvent)
        {
            var settings = await LoadSettings().ConfigureAwait(false);
            if (settings == null || !settings.Email.Enabled)
            {
                return;
            }

            await EmailSender.Send(
                    settings.Email,
                    "Health check succeeded",
                    $"Health check {domainEvent.Category}: {domainEvent.CustomCheckId} succeeded at {domainEvent.SucceededAt}.",
                    emailDropFolder)
                .ConfigureAwait(false);
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
