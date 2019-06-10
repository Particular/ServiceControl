namespace ServiceControl.Audit.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Auditing;
    using NServiceBus;
    using ServiceControl.Contracts.EndpointControl;

    class DetectNewEndpointsFromAuditImportsEnricher : IEnrichImportedAuditMessages
    {
        public DetectNewEndpointsFromAuditImportsEnricher(EndpointInstanceMonitoring monitoring)
        {
            this.monitoring = monitoring;
        }

        public async Task Enrich(AuditEnricherContext context)
        {
            var headers = context.Headers;
            var metadata = context.Metadata;
            var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(headers);

            // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
            // have the relevant information via the headers, which were added in v4.
            if (sendingEndpoint != null)
            {
                await TryAddEndpoint(sendingEndpoint, context.MessageSession)
                    .ConfigureAwait(false);
                metadata.Add("SendingEndpoint", sendingEndpoint);
            }

            var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(headers);
            // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
            // processed because we dont have the information from the relevant headers.
            if (receivingEndpoint != null)
            {
                metadata.Add("ReceivingEndpoint", receivingEndpoint);
                await TryAddEndpoint(receivingEndpoint, context.MessageSession)
                    .ConfigureAwait(false);
            }
        }

        async Task TryAddEndpoint(EndpointDetails endpointDetails, IMessageSession messageSession)
        {
            // for backwards compat with version before 4_5 we might not have a hostid
            if (endpointDetails.HostId == Guid.Empty)
            {
                return;
            }

            if (monitoring.IsNewInstance(endpointDetails))
            {
                await messageSession.Publish(new NewEndpointDetected
                {
                    DetectedAt = DateTime.UtcNow,
                    Endpoint = endpointDetails
                }).ConfigureAwait(false);
            }
        }

        EndpointInstanceMonitoring monitoring;
    }
}