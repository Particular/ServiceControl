namespace ServiceControl.Notifications.Mail
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class SendEmailNotificationHandler : IHandleMessages<SendEmailNotification>
    {
        readonly IDocumentStore store;

        public SendEmailNotificationHandler(IDocumentStore store, Settings settings)
        {
            this.store = store;
            emailDropFolder = settings.EmailDropFolder;
        }

        public async Task Handle(SendEmailNotification message, IMessageHandlerContext context)
        {
            using (var session = store.OpenAsyncSession())
            {
                var notifications = await session.LoadAsync<NotificationsSettings>(NotificationsSettings.SingleDocumentId)
                    .ConfigureAwait(false);

                await TrySendingEmailNotification(notifications, message.Subject, message.Body).ConfigureAwait(false);
            }
        }

        async Task TrySendingEmailNotification(NotificationsSettings settings, string subject, string body)
        {
            if (settings == null || !settings.Email.Enabled)
            {
                log.Info("Skipping email sending. Notifications turned-off.");
                return;
            }

            await EmailSender.Send(settings.Email, subject, body, emailDropFolder)
                .ConfigureAwait(false);
        }

        static ILog log = LogManager.GetLogger<SendEmailNotificationHandler>();
        string emailDropFolder;
    }
}