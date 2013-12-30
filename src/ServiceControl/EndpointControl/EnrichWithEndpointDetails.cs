namespace ServiceControl.EndpointControl
{
    using Contracts.Operations;
    using Operations;

    public class EnrichWithEndpointDetails : ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {
            var sendingEndpoint = EndpointDetails.SendingEndpoint(message.PhysicalMessage.Headers);

            message.Metadata.Add("SendingEndpoint", sendingEndpoint);

            var receivingEndpoint = EndpointDetails.ReceivingEndpoint(message.PhysicalMessage.Headers);

            message.Metadata.Add("ReceivingEndpoint", receivingEndpoint);
        }
    }
}