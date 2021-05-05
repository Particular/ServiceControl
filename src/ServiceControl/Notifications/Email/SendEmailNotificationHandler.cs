namespace ServiceControl.Notifications.Email
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

            var cancellationToken = throttlingState.CancellationTokenSource.Token;

            var hasSemaphore = false;

            try
            {
                while (hasSemaphore == false)
                {
                    if (throttlingState.IsThrottling())
                    {
                        log.Warn("Email notification throttled");
                        return;
                    }

                    hasSemaphore = await throttlingState.Semaphore.WaitAsync(spinDelay, cancellationToken).ConfigureAwait(false);
                }

                if (context.MessageId == throttlingState.RetriedMessageId)
                {
                    message.Body +=
                        "\n\nWARNING: Your SMTP server was temporarily unavailable. Make sure to check ServicePulse for a full list of health check notifications.";
                }

                await EmailSender.Send(notifications.Email, message.Subject, message.Body, emailDropFolder)
                    .ConfigureAwait(false);
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                if (message.FailureNotification)
                {
                    throttlingState.ThrottlingOn();

                    await Task.Delay(throttlingDelay, cancellationToken).ConfigureAwait(false);

                    throttlingState.ThrottlingOff();

                    throttlingState.RetriedMessageId = context.MessageId;

                    throw new EmailNotificationException(e);
                }
                else
                {
                    log.Warn("Success notification skipped due to throttling.");
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