namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class RabbitMQConventionalRoutingTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<RabbitMQTransport>();
            ConfigureTransport(transport, transportSettings);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<RabbitMQTransport>();
            ConfigureTransport(transport, transportSettings);
        }

        static void ConfigureTransport(TransportExtensions<RabbitMQTransport> transport, TransportSettings transportSettings)
        {
            transport.UseConventionalRoutingTopology();
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ConnectionString(transportSettings.ConnectionString);
        }
    }
}