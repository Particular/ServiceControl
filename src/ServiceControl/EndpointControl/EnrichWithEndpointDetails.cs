namespace ServiceControl.EndpointControl
{
    using Operations;
    using ServiceControl.Contracts.Operations;

    public class EnrichWithEndpointDetails : ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {
            var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(message.PhysicalMessage.Headers);
            // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
            // have the relevant information via the headers, which were added in v4.
            if (sendingEndpoint != null)
            {
                message.Metadata.Add("SendingEndpoint", sendingEndpoint);
            }

            // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
            // processed because we dont have the information from the relevant headers.
            var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(message.PhysicalMessage.Headers);
            if (receivingEndpoint != null)
            {
                message.Metadata.Add("ReceivingEndpoint", receivingEndpoint);
            }
        }
    }
}