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
            if (settings == null || !settings.AlertingEnabled)
            {
                return;
            }

            await EmailSender.Send(
                    settings,
                "Health check failed",
                $"Health check {domainEvent.Category}: {domainEvent.CustomCheckId} failed at {domainEvent.FailedAt}. Failure reason {domainEvent.FailureReason}",
                    emailDropFolder)
                .ConfigureAwait(false);
        }

        public async Task Handle(CustomCheckSucceeded domainEvent)
        {
            var settings = await LoadSettings().ConfigureAwait(false);
            if (settings == null || !settings.AlertingEnabled)
            {
                return;
            }

            await EmailSender.Send(
                    settings,
                    "Health check succeeded",
                    $"Health check {domainEvent.Category}: {domainEvent.CustomCheckId} succeeded at {domainEvent.SucceededAt}.",
                    emailDropFolder)
                .ConfigureAwait(false);
        }

        async Task<AlertingSettings> LoadSettings()
        {
            using (var session = store.OpenAsyncSession())
            {
                return await session.LoadAsync<AlertingSettings>(AlertingSettings.SingleDocumentId).ConfigureAwait(false);
            }
        }
    }
}
