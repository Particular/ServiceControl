namespace ServiceControl.Transports.ASB
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBForwardingTopologyTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, string connectionString)
        {           
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            ConfigureTransport(transport, connectionString);
            CustomizeEndpointTransport(transport);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, string connectionString)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.ApplyHacksForNsbRaw();
            ConfigureTransport(transport, connectionString);
            CustomizeRawEndpointTransport(transport);
        }
        
        protected virtual void CustomizeEndpointTransport(TransportExtensions<AzureServiceBusTransport> extensions)
        {
        }

        protected virtual void CustomizeRawEndpointTransport(TransportExtensions<AzureServiceBusTransport> extensions)
        {
        }
        
        static void ConfigureTransport(TransportExtensions<AzureServiceBusTransport> transport, string connectionString)
        {
            transport.UseForwardingTopology();
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ConnectionString(connectionString);
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
        }
    }
}