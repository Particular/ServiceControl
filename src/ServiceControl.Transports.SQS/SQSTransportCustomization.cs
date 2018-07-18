namespace ServiceControl.Transports.SQS
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class SQSTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {           
            var transport = endpointConfig.UseTransport<SqsTransport>();

            ConfigureTransport(transport, transportSettings);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<SqsTransport>();
            
            ConfigureTransport(transport, transportSettings);
        }
                
        static void ConfigureTransport(TransportExtensions<SqsTransport> transport, TransportSettings transportSettings)
        {
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
        }
    }
}