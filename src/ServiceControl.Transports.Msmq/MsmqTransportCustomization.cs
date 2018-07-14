namespace ServiceControl.Transports.Msmq
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class MsmqTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<MsmqTransport>();
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<MsmqTransport>();
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        }
    }
}