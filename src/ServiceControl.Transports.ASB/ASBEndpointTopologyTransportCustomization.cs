namespace ServiceControl.Transports.ASB
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBEndpointTopologyTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
            var topology = transport.UseEndpointOrientedTopology();
            topology.EnableMigrationToForwardingTopology();

            transport.ConfigureTransport(transportSettings);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.UseEndpointOrientedTopology();
            transport.ApplyHacksForNsbRaw();

            transport.ConfigureTransport(transportSettings);
        }
    }
}