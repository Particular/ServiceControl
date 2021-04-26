namespace ServiceControl.Notifications.Mail
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;
    using NServiceBus.Transport;

    class EmailNotificationThrottlingBehavior : Behavior<IIncomingLogicalMessageContext>
    {
        public EmailNotificationThrottlingBehavior(SemaphoreSlim semaphore) => this.semaphore = semaphore;

        public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
        {
            if (!(context.Message.Instance is SendEmailNotification))
            {
                await next().ConfigureAwait(false);
                return;
            }

            try
            {
                await semaphore.WaitAsync().ConfigureAwait(false);

                await next().ConfigureAwait(false);

            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                log.Warn($"Error sending email notification. Backing-off for {backoffDelay}", e);

                await Task.Delay(backoffDelay).ConfigureAwait(false);

                throw new EmailNotificationSendException(e);
            }
            finally
            {
                semaphore.Release();
            }
        }


        public static RecoverabilityAction RecoverabilityPolicy(RecoverabilityConfig config, ErrorContext context)
        {
            var action = DefaultRecoverabilityPolicy.Invoke(config, context);

            if (context.Exception is EmailNotificationSendException)
            {
                if (context.ImmediateProcessingFailures <= maxNumberOfFailures)
                {
                    return RecoverabilityAction.ImmediateRetry();
                }

                return RecoverabilityAction.Discard("Cannot deliver an email notification.");
            }

            return action;
        }

        class EmailNotificationSendException : Exception
        {
            public EmailNotificationSendException(Exception exception) : base("Error sending email notification", exception)
            {
            }
        }

        readonly SemaphoreSlim semaphore;

        static TimeSpan backoffDelay = TimeSpan.FromMinutes(1);
        static int maxNumberOfFailures = 10;
        static ILog log = LogManager.GetLogger<EmailNotificationThrottlingBehavior>();
    }
}