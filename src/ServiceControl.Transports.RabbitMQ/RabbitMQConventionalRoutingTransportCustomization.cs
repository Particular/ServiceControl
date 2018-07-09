namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;
    using NServiceBus.Raw;
    using ServiceControl.Infrastructure.Transport;

    public class RabbitMQConventionalRoutingTransportCustomization : TransportCustomization
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, string connectionString)
        {
            var transport = endpointConfig.UseTransport<RabbitMQTransport>();
            ConfigureTransport(transport, connectionString);
            CustomizeEndpointTransport(transport);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, string connectionString)
        {
            var transport = endpointConfig.UseTransport<RabbitMQTransport>();
            ConfigureTransport(transport, connectionString);
            CustomizeRawEndpointTransport(transport);
        }

        protected virtual void CustomizeEndpointTransport(TransportExtensions<RabbitMQTransport> extensions)
        {
        }

        protected virtual void CustomizeRawEndpointTransport(TransportExtensions<RabbitMQTransport> extensions)
        {
        }

        static void ConfigureTransport(TransportExtensions<RabbitMQTransport> transport, string connectionString)
        {
            transport.UseConventionalRoutingTopology();
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ConnectionString(connectionString);
        }
    }
}