namespace ServiceControl.Notifications.Mail
{
    using System;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Infrastructure.DomainEvents;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    class CustomChecksMailNotification : IDomainHandler<CustomCheckFailed>, IDomainHandler<CustomCheckSucceeded>
    {
        readonly IDocumentStore store;
        readonly string emailDropFolder;
        readonly string instanceName;
        string instanceAddress;

        public CustomChecksMailNotification(IDocumentStore store, Settings settings)
        {
            this.store = store;

            emailDropFolder = settings.EmailDropFolder;
            instanceName = settings.ServiceName;
            instanceAddress = settings.ApiUrl;
        }

        public Task Handle(CustomCheckFailed domainEvent)
        {
            var subject = $"[{instanceName}] Health check failed";
            var body = $@"Service Control instance: {instanceName} at {instanceAddress}.
Health check {domainEvent.Category}: {domainEvent.CustomCheckId} failed at {domainEvent.FailedAt}. Failure reason {domainEvent.FailureReason}";

            return TrySendingEmailNotification(subject, body);
        }

        public Task Handle(CustomCheckSucceeded domainEvent)
        {
            var subject = $"[{instanceName}] Health check succeeded";
            var body = $@"Service Control instance: {instanceName} at {instanceAddress}.
Health check {domainEvent.Category}: {domainEvent.CustomCheckId} succeeded at {domainEvent.SucceededAt}.";

            return TrySendingEmailNotification(subject, body);
        }

        async Task TrySendingEmailNotification(string subject, string body)
        {
            var notificationsSettings = await LoadSettings().ConfigureAwait(false);
            if (notificationsSettings == null || !notificationsSettings.Email.Enabled)
            {
                log.Info("Skipping email sending. Notifications turned-off.");
                return;
            }

            _ = Task.Run(async () =>
              {
                  try
                  {
                      await EmailSender.Send(notificationsSettings.Email, subject, body, emailDropFolder)
                          .ConfigureAwait(false);
                  }
                  catch (Exception e)
                  {
                      log.Warn("Failure sending email notification.", e);
                  }
              });
        }

        async Task<NotificationsSettings> LoadSettings()
        {
            using (var session = store.OpenAsyncSession())
            {
                return await session.LoadAsync<NotificationsSettings>(NotificationsSettings.SingleDocumentId).ConfigureAwait(false);
            }
        }

        static ILog log = LogManager.GetLogger<CustomChecksMailNotification>();
    }
}
