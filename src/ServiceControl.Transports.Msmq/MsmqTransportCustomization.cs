namespace ServiceControl.Transports.Msmq
{
    using NServiceBus;
    using NServiceBus.Raw;
    using ServiceControl.Infrastructure.Transport;

    public class MsmqTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, string connectionString)
        {
            var transport = endpointConfig.UseTransport<MsmqTransport>();
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            CustomizeEndpointTransport(transport);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, string connectionString)
        {
            var transport = endpointConfig.UseTransport<MsmqTransport>();
            transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
            CustomizeRawEndpointTransport(transport);
        }

        protected virtual void CustomizeEndpointTransport(TransportExtensions<MsmqTransport> extensions)
        {
        }

        protected virtual void CustomizeRawEndpointTransport(TransportExtensions<MsmqTransport> extensions)
        {
        }
    }
}