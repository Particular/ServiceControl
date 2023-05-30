﻿namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using NServiceBus;
    using Plugin.CustomChecks.Messages;
    using ServiceControl.Operations;

    class ReportCustomCheckResultHandler : IHandleMessages<ReportCustomCheckResult>
    {
        public ReportCustomCheckResultHandler(CustomCheckResultProcessor checkResultProcessor)
        {
            this.checkResultProcessor = checkResultProcessor;
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

            var checkDetails = new CustomCheckDetail
            {
                OriginatingEndpoint = new EndpointDetails
                {
                    Host = message.Host,
                    HostId = message.HostId,
                    Name = message.EndpointName
                },
                ReportedAt = message.ReportedAt,
                CustomCheckId = message.CustomCheckId,
                Category = message.Category,
                HasFailed = message.HasFailed,
                FailureReason = message.FailureReason
            };

            await checkResultProcessor.ProcessResult(checkDetails).ConfigureAwait(false);
        }

        readonly CustomCheckResultProcessor checkResultProcessor;
    }
}