namespace ServiceControl.Transports.ASB
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBForwardingTopologyTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();

            transport.UseForwardingTopology();
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
            transport.ConfigureTransport(transportSettings);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.ApplyHacksForNsbRaw();

            transport.UseForwardingTopology();
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
            transport.ConfigureTransport(transportSettings);
        }
    }
}