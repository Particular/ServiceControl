namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using NServiceBus;
    using Plugin.CustomChecks.Messages;

    class ReportCustomCheckResultHandler : IHandleMessages<ReportCustomCheckResult>
    {
        public ReportCustomCheckResultHandler(CustomChecksStorage customChecks)
        {
            this.customChecks = customChecks;
        }

        public async Task Handle(ReportCustomCheckResult message, IMessageHandlerContext context)
        {
            if (string.IsNullOrEmpty(message.EndpointName))
            {
                throw new Exception("Received an custom check message without proper initialization of the EndpointName in the schema");
            }

            if (string.IsNullOrEmpty(message.Host))
            {
                throw new Exception("Received an custom check message without proper initialization of the Host in the schema");
            }

            if (message.HostId == Guid.Empty)
            {
                throw new Exception("Received an custom check message without proper initialization of the HostId in the schema");
            }

            var originatingEndpoint = new EndpointDetails
            {
                Host = message.Host,
                HostId = message.HostId,
                Name = message.EndpointName
            };

            await customChecks.UpdateCustomCheckStatus(
                    originatingEndpoint,
                    message.ReportedAt,
                    message.CustomCheckId,
                    message.Category,
                    message.HasFailed,
                    message.FailureReason
                ).ConfigureAwait(false);
        }

        CustomChecksStorage customChecks;
    }
}