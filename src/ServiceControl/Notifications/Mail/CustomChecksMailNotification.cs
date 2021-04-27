namespace ServiceControl.Notifications.Mail
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class CustomChecksMailNotification : IDomainHandler<CustomCheckFailed>, IDomainHandler<CustomCheckSucceeded>
    {
        readonly IMessageSession messageSession;
        readonly EmailThrottlingState throttlingState;
        readonly string instanceName;
        string instanceAddress;
        string[] serviceControlHealthCustomCheckIds = {
            "Audit Message Ingestion Process",
            "Audit Message Ingestion",
            "ServiceControl.Audit database",
            "Dead Letter Queue",
            "ServiceControl Primary Instance",
            "ServiceControl database",
            "ServiceControl Remotes",
            "Error Message Ingestion Process",
            "Audit Message Ingestion",
            "Error Message Ingestion"
        };

        public CustomChecksMailNotification(IMessageSession messageSession, Settings settings, EmailThrottlingState throttlingState)
        {
            this.messageSession = messageSession;
            this.throttlingState = throttlingState;

            instanceName = settings.ServiceName;
            instanceAddress = settings.ApiUrl;

            if (string.IsNullOrWhiteSpace(settings.NotificationsFilter) == false)
            {
                serviceControlHealthCustomCheckIds = settings.NotificationsFilter.Split(new[] { '#' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public Task Handle(CustomCheckFailed domainEvent)
        {
            if (IsHealthCheck(domainEvent.CustomCheckId))
            {
                return messageSession.SendLocal(new SendEmailNotification
                {
                    FailureNumber = throttlingState.NextFailure(),
                    Subject = $"[{instanceName}] Health check failed",
                    Body = $@"Service Control instance: {instanceName} at {instanceAddress}.
Health check {domainEvent.Category}: {domainEvent.CustomCheckId} failed at {domainEvent.FailedAt}. Failure reason {domainEvent.FailureReason}"
                });
            }

            return Task.CompletedTask;
        }

        public Task Handle(CustomCheckSucceeded domainEvent)
        {
            if (IsHealthCheck(domainEvent.CustomCheckId))
            {
                return messageSession.SendLocal(new SendEmailNotification
                {
                    Subject = $"[{instanceName}] Health check succeeded",
                    Body = $@"Service Control instance: {instanceName} at {instanceAddress}.
Health check {domainEvent.Category}: {domainEvent.CustomCheckId} succeeded at {domainEvent.SucceededAt}."
                });
            }

            return Task.CompletedTask;
        }

        bool IsHealthCheck(string checkId) => serviceControlHealthCustomCheckIds.Any(id => string.Equals(id, checkId, StringComparison.InvariantCultureIgnoreCase));
    }
}
