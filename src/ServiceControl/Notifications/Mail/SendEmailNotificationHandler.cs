namespace ServiceControl.Notifications.Mail
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class SendEmailNotificationHandler : IHandleMessages<SendEmailNotification>
    {
        readonly IDocumentStore store;
        readonly EmailThrottlingState throttlingState;

        public SendEmailNotificationHandler(IDocumentStore store, Settings settings, EmailThrottlingState throttlingState)
        {
            this.store = store;
            this.throttlingState = throttlingState;

            emailDropFolder = settings.EmailDropFolder;
        }

        public async Task Handle(SendEmailNotification message, IMessageHandlerContext context)
        {
            NotificationsSettings notifications;

            using (var session = store.OpenAsyncSession())
            {
                notifications = await session.LoadAsync<NotificationsSettings>(NotificationsSettings.SingleDocumentId)
                    .ConfigureAwait(false);
            }

            if (notifications == null || !notifications.Email.Enabled)
            {
                log.Info("Skipping email sending. Notifications turned-off.");
                return;
            }

            var hasSemaphore = false;

            try
            {
                while (hasSemaphore == false)
                {
                    if (throttlingState.IsThrottling())
                    {
                        log.Warn("Email notifications throttled");
                        return;
                    }

                    hasSemaphore = await throttlingState.Semaphore.WaitAsync(spinDelay).ConfigureAwait(false);
                }

                await EmailSender.Send(notifications.Email, message.Subject, message.Body, emailDropFolder)
                    .ConfigureAwait(false);
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                if (message.IsFailure)
                {
                    throttlingState.ThrottlingOn();

                    await Task.Delay(throttlingDelay).ConfigureAwait(false);

                    throttlingState.ThrottlingOff();

                    throw new EmailNotificationException(e);
                }
                else
                {
                    log.Warn("Email notification throttled.");
                }
            }
            finally
            {
                if (hasSemaphore)
                {
                    throttlingState.Semaphore.Release();
                }
            }
        }

        string emailDropFolder;

        static ILog log = LogManager.GetLogger<SendEmailNotificationHandler>();
        static TimeSpan spinDelay = TimeSpan.FromSeconds(1);
        static TimeSpan throttlingDelay = TimeSpan.FromMinutes(1);

        public static RecoverabilityAction RecoverabilityPolicy(RecoverabilityConfig config, ErrorContext context)
        {
            if (context.Exception is EmailNotificationException)
            {
                return RecoverabilityAction.ImmediateRetry();
            }

            return DefaultRecoverabilityPolicy.Invoke(config, context);
        }
    }

    class EmailNotificationException : Exception
    {
        public EmailNotificationException(Exception exception) : base("Error sending email notification.", exception)
        {
        }
    }
}