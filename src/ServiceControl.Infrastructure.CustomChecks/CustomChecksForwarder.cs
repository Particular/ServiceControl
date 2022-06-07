namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using NServiceBus;
    using Plugin.CustomChecks.Messages;

    class CustomChecksForwarder : ICustomChecksBackend
    {
        readonly IMessageSession messageSession;
        readonly string forwardTo;

        public CustomChecksForwarder(IMessageSession messageSession, string forwardTo)
        {
            this.messageSession = messageSession;
            this.forwardTo = forwardTo;
        }

        public Task UpdateCustomCheckStatus(EndpointDetails originatingEndpoint, DateTime reportedAt, string customCheckId, string category, bool hasFailed, string failureReason)
        {
            var message = new ReportCustomCheckResult
            {
                ReportedAt = reportedAt,
                Category = category,
                CustomCheckId = customCheckId,
                EndpointName = originatingEndpoint.Name,
                FailureReason = failureReason,
                HasFailed = hasFailed,
                Host = originatingEndpoint.Host,
                HostId = originatingEndpoint.HostId
            };
            var sendOptions = new SendOptions();
            sendOptions.SetDestination(forwardTo);
            return messageSession.Send(message, sendOptions);
        }
    }
}