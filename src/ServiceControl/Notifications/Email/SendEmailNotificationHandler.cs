namespace ServiceControl.Notifications.Email
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NServiceBus;
    using NServiceBus.Transport;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    class SendEmailNotificationHandler : IHandleMessages<SendEmailNotification>
    {
        readonly IErrorMessageDataStore store;
        readonly EmailThrottlingState throttlingState;
        readonly EmailSender emailSender;

        public SendEmailNotificationHandler(IErrorMessageDataStore store, IOptions<PrimaryOptions> settings, EmailThrottlingState throttlingState, EmailSender emailSender, ILogger<SendEmailNotificationHandler> logger)
        {
            this.store = store;
            this.throttlingState = throttlingState;
            this.emailSender = emailSender;
            this.logger = logger;
            emailDropFolder = settings.Value.EmailDropFolder;
        }

        public async Task Handle(SendEmailNotification message, IMessageHandlerContext context)
        {
            NotificationsSettings notifications;

            using (var manager = await store.CreateNotificationsManager())
            {
                notifications = await manager.LoadSettings(cacheTimeout);
            }

            if (notifications == null || !notifications.Email.Enabled)
            {
                logger.LogInformation("Skipping email sending. Notifications turned-off");
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
                        logger.LogWarning("Email notification throttled");
                        return;
                    }

                    hasSemaphore = await throttlingState.Semaphore.WaitAsync(spinDelay, cancellationToken);
                }

                if (context.MessageId == throttlingState.RetriedMessageId)
                {
                    message.Body +=
                        "\n\nWARNING: Your SMTP server was temporarily unavailable. Make sure to check ServicePulse for a full list of health check notifications.";
                }

                await emailSender.Send(notifications.Email, message.Subject, message.Body, emailDropFolder);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                if (message.FailureNotification)
                {
                    throttlingState.ThrottlingOn();

                    await Task.Delay(throttlingDelay, cancellationToken);

                    throttlingState.ThrottlingOff();

                    throttlingState.RetriedMessageId = context.MessageId;

                    throw new EmailNotificationException(e);
                }
                else
                {
                    logger.LogWarning("Success notification skipped due to throttling");
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

        readonly ILogger<SendEmailNotificationHandler> logger;
        static TimeSpan spinDelay = TimeSpan.FromSeconds(1);
        static TimeSpan throttlingDelay = TimeSpan.FromSeconds(30);
        static TimeSpan cacheTimeout = TimeSpan.FromMinutes(5);

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