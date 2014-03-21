namespace ServiceControl.EndpointControl
{
    using Operations;
    using ServiceControl.Contracts.Operations;

    public class EnrichWithEndpointDetails : ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {
            var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(message.PhysicalMessage.Headers);

            message.Metadata.Add("SendingEndpoint", sendingEndpoint);

            var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(message.PhysicalMessage.Headers);

            message.Metadata.Add("ReceivingEndpoint", receivingEndpoint);
        }
    }
}