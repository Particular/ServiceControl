namespace ServiceControl.Notifications.Email
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class CustomChecksMailNotification : IDomainHandler<CustomCheckFailed>, IDomainHandler<CustomCheckSucceeded>
    {
        readonly IMessageSession messageSession;
        readonly Settings settings;
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

        public CustomChecksMailNotification(IMessageSession messageSession, IOptions<Settings> settingsOptions, EmailThrottlingState throttlingState, ILogger<CustomChecksMailNotification> logger)
        {
            this.messageSession = messageSession;
            this.throttlingState = throttlingState;
            this.logger = logger;
            settings = settingsOptions.Value;
            instanceName = settings.ServiceControl.InstanceName;
            instanceAddress = settings.ServiceControl.ApiUrl;

            if (string.IsNullOrWhiteSpace(settings.ServiceControl.NotificationsFilter) == false)
            {
                serviceControlHealthCustomCheckIds = NotificationsFilterParser.Parse(settings.ServiceControl.NotificationsFilter);
            }
        }

        public Task Handle(CustomCheckFailed domainEvent, CancellationToken cancellationToken)
        {
            if (IsHealthCheck(domainEvent.CustomCheckId))
            {
                if (throttlingState.IsThrottling())
                {
                    logger.LogWarning("Email notification throttled");
                    return Task.CompletedTask;
                }

                return messageSession.SendLocal(new SendEmailNotification
                {
                    FailureNotification = true,
                    Subject = $"[{instanceName}] health check failed",
                    Body = $@"{domainEvent.Category} check for ServiceControl instance {instanceName} at {instanceAddress}.

{domainEvent.CustomCheckId} failed on {domainEvent.FailedAt}. 

{domainEvent.FailureReason}"
                }, cancellationToken: cancellationToken);
            }

            return Task.CompletedTask;
        }

        public Task Handle(CustomCheckSucceeded domainEvent, CancellationToken cancellationToken)
        {
            if (IsHealthCheck(domainEvent.CustomCheckId))
            {
                if (throttlingState.IsThrottling())
                {
                    logger.LogWarning("Email notification throttled");
                    return Task.CompletedTask;
                }

                return messageSession.SendLocal(new SendEmailNotification
                {
                    FailureNotification = false,
                    Subject = $"[{instanceName}] health check succeeded",
                    Body = $@"{domainEvent.Category} check for ServiceControl instance {instanceName} at {instanceAddress}.

{domainEvent.CustomCheckId} succeeded on {domainEvent.SucceededAt}."
                }, cancellationToken: cancellationToken);
            }

            return Task.CompletedTask;
        }

        bool IsHealthCheck(string checkId) => serviceControlHealthCustomCheckIds.Any(id => string.Equals(id, checkId, StringComparison.InvariantCultureIgnoreCase));

        readonly ILogger<CustomChecksMailNotification> logger;
    }
}
