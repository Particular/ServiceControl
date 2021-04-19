namespace ServiceControl.Infrastructure.Mail
{
    using System.Net;
    using System.Net.Mail;
    using System.Threading.Tasks;
    using Alerting;
    using Contracts.CustomChecks;
    using DomainEvents;
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

            var smtpClient = CreateSmtpClient(settings);

            var mailMessage = new MailMessage(settings.From, settings.To, "Health check failed",
                $"Health check {domainEvent.Category}: {domainEvent.CustomCheckId} failed at {domainEvent.FailedAt}. Failure reason {domainEvent.FailureReason}");

            await smtpClient.SendMailAsync(mailMessage).ConfigureAwait(false);
        }

        SmtpClient CreateSmtpClient(AlertingSettings settings)
        {
            if (emailDropFolder != null)
            {
                return new SmtpClient
                {
                    PickupDirectoryLocation = emailDropFolder,
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory
                };
            }

            var smtpClient = new SmtpClient(settings.SmtpServer, settings.SmtpPort ?? 25)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = settings.EnableSSL,
            };
            if (settings.AuthenticationEnabled)
            {
                smtpClient.Credentials = new NetworkCredential(settings.AuthenticationAccount, settings.AuthenticationPassword);
            }
            return smtpClient;
        }

        public async Task Handle(CustomCheckSucceeded domainEvent)
        {
            var settings = await LoadSettings().ConfigureAwait(false);
            if (settings == null || !settings.AlertingEnabled)
            {
                return;
            }

            var smtpClient = CreateSmtpClient(settings);

            var mailMessage = new MailMessage(settings.From, settings.To, "Health check succeeded",
                $"Health check {domainEvent.Category}: {domainEvent.CustomCheckId} succeeded at {domainEvent.SucceededAt}.");

            await smtpClient.SendMailAsync(mailMessage).ConfigureAwait(false);
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
