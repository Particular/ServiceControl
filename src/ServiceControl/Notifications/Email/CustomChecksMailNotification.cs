namespace ServiceControl.Notifications.Email
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

        public CustomChecksMailNotification(IMessageSession messageSession, Settings settings)
        {
            this.messageSession = messageSession;

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
                    IsFailure = true,
                    Subject = $"[{instanceName}] health check failed",
                    Body = $@"{domainEvent.Category} check for ServiceControl instance {instanceName} at {instanceAddress}.

{domainEvent.CustomCheckId} failed on {domainEvent.FailedAt}. 

{domainEvent.FailureReason}"
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
                    IsFailure = false,
                    Subject = $"[{instanceName}] health check succeeded",
                    Body = $@"{domainEvent.Category} check for ServiceControl instance {instanceName} at {instanceAddress}.

{domainEvent.CustomCheckId} succeeded on {domainEvent.SucceededAt}."
                });
            }

            return Task.CompletedTask;
        }

        bool IsHealthCheck(string checkId) => serviceControlHealthCustomCheckIds.Any(id => string.Equals(id, checkId, StringComparison.InvariantCultureIgnoreCase));
    }
}
