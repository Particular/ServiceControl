namespace ServiceControl.Auditing
{
    using System;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class DetectNewEndpointsFromAuditImportsEnricher(IEndpointInstanceMonitoring monitoring) : IEnrichImportedAuditMessages
    {
        public void Enrich(AuditEnricherContext context)
        {
            var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(context.Headers);

            // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
            // have the relevant information via the headers, which were added in v4.
            if (sendingEndpoint != null)
            {
                TryAddEndpoint(sendingEndpoint, context);
                context.Metadata.Add("SendingEndpoint", sendingEndpoint);
            }

            var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(context.Headers);
            // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
            // processed because we dont have the information from the relevant headers.
            if (receivingEndpoint != null)
            {
                TryAddEndpoint(receivingEndpoint, context);
                context.Metadata.Add("ReceivingEndpoint", receivingEndpoint);
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
                context.Add(endpointDetails);
            }
        }
    }
}
