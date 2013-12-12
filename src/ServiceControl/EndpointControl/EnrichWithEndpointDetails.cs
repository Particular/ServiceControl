namespace ServiceControl.EndpointControl
{
    using Contracts.Operations;
    using Operations;

    public class EnrichWithEndpointDetails : ImportEnricher
    {

        public override void Enrich(ImportMessage message)
        {
            var sendingEndpoint = EndpointDetails.SendingEndpoint(message.PhysicalMessage.Headers);

            message.Add(new MessageMetadata("SendingEndpoint", sendingEndpoint.Name));

            var receivingEndpoint = EndpointDetails.ReceivingEndpoint(message.PhysicalMessage.Headers);

            message.Add(new MessageMetadata("ReceivingEndpoint", receivingEndpoint.Name));
        }
    }


}