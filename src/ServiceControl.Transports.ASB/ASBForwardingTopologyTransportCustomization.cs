namespace ServiceControl.Transports.ASB
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBForwardingTopologyTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {           
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            ConfigureTransport(transport, transportSettings);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.ApplyHacksForNsbRaw();
            ConfigureTransport(transport, transportSettings);
        }
                
        static void ConfigureTransport(TransportExtensions<AzureServiceBusTransport> transport, TransportSettings transportSettings)
        {
            transport.UseForwardingTopology();
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ConnectionString(transportSettings.ConnectionString);
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
        }
    }
}