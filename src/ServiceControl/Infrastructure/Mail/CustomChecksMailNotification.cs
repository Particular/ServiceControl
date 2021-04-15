namespace ServiceControl.Infrastructure.Mail
{
    using System.Net.Mail;
    using System.Threading.Tasks;
    using Alerting;
    using Contracts.CustomChecks;
    using DomainEvents;
    using Raven.Client;

    class CustomChecksMailNotification : IDomainHandler<CustomCheckFailed>, IDomainHandler<CustomCheckSucceeded>
    {
        readonly IDocumentStore store;

        public CustomChecksMailNotification(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task Handle(CustomCheckFailed domainEvent)
        {
            var settings = await LoadSettings().ConfigureAwait(false);
            if (settings == null || !settings.AlertingEnabled)
            {
                return;
            }

            var smtpClient = new SmtpClient(settings.SmtpServer, settings.SmtpPort ?? 25);

            var mailMessage = new MailMessage(settings.From, settings.To, "Health check failed",
                $"Health check {domainEvent.Category}: {domainEvent.CustomCheckId} failed at {domainEvent.FailedAt}. Failure reason {domainEvent.FailureReason}");

            await smtpClient.SendMailAsync(mailMessage).ConfigureAwait(false);
        }

        public async Task Handle(CustomCheckSucceeded domainEvent)
        {
            var settings = await LoadSettings().ConfigureAwait(false);
            if (settings == null || !settings.AlertingEnabled)
            {
                return;
            }

            var smtpClient = new SmtpClient(settings.SmtpServer, settings.SmtpPort ?? 25);

            var mailMessage = new MailMessage(settings.From, settings.To, "Health check succeeded",
                $"Health check {domainEvent.Category}: {domainEvent.CustomCheckId} succeeded at {domainEvent.SucceededAt}.");

            await smtpClient.SendMailAsync(mailMessage).ConfigureAwait(false);
        }

        async Task<AlertingSettings> LoadSettings()
        {
            using (var session = store.OpenAsyncSession())
            {
                return await session.LoadAsync<AlertingSettings>(DocumentId).ConfigureAwait(false);
            }
        }

        const string DocumentId = "AlertingSettings/1";
    }
}
