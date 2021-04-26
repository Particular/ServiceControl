namespace ServiceControl.Notifications.Mail
{
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

        public CustomChecksMailNotification(IMessageSession messageSession, Settings settings)
        {
            this.messageSession = messageSession;
            instanceName = settings.ServiceName;
            instanceAddress = settings.ApiUrl;
        }

        public Task Handle(CustomCheckFailed domainEvent) =>
            messageSession.SendLocal(new SendEmailNotification
            {
                Subject = $"[{instanceName}] Health check failed",
                Body = $@"Service Control instance: {instanceName} at {instanceAddress}.
Health check {domainEvent.Category}: {domainEvent.CustomCheckId} failed at {domainEvent.FailedAt}. Failure reason {domainEvent.FailureReason}"
            });

        public Task Handle(CustomCheckSucceeded domainEvent) =>
            messageSession.SendLocal(new SendEmailNotification
            {
                Subject = $"[{instanceName}] Health check succeeded",
                Body = $@"Service Control instance: {instanceName} at {instanceAddress}.
Health check {domainEvent.Category}: {domainEvent.CustomCheckId} succeeded at {domainEvent.SucceededAt}."
            });
    }
}
