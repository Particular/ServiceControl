namespace ServiceControl.Audit.Monitoring
{
    using System;
    using Auditing;
    using Contracts.EndpointControl;

    class DetectNewEndpointsFromAuditImportsEnricher : IEnrichImportedAuditMessages
    {
        public DetectNewEndpointsFromAuditImportsEnricher(EndpointInstanceMonitoring monitoring)
        {
            this.monitoring = monitoring;
        }

        public void Enrich(AuditEnricherContext context)
        {
            var headers = context.Headers;
            var metadata = context.Metadata;
            var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(headers);

            // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
            // have the relevant information via the headers, which were added in v4.
            if (sendingEndpoint != null)
            {
                TryAddEndpoint(sendingEndpoint, context);

                metadata.Add("SendingEndpoint", sendingEndpoint);
            }

            var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(headers);
            // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
            // processed because we dont have the information from the relevant headers.
            if (receivingEndpoint != null)
            {
                TryAddEndpoint(receivingEndpoint, context);

                metadata.Add("ReceivingEndpoint", receivingEndpoint);
            }
        }

        void TryAddEndpoint(EndpointDetails endpointDetails, AuditEnricherContext context)
        {
            // for backwards compat with version before 4_5 we might not have a hostid
            if (endpointDetails.HostId == Guid.Empty)
            {
                return;
            }

            if (monitoring.IsNewInstance(endpointDetails))
            {
                context.AddForSend(new RegisterNewEndpoint
                {
                    DetectedAt = DateTime.UtcNow,
                    Endpoint = endpointDetails
                });
            }
        }

        EndpointInstanceMonitoring monitoring;
    }
}