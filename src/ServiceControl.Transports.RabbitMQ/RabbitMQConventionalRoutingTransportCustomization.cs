namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class RabbitMQConventionalRoutingTransportCustomization : TransportCustomization
    {
        public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        static void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<RabbitMQTransport>();
            ConfigureTransport(transport, transportSettings);
        }

        static void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<RabbitMQTransport>();
            ConfigureTransport(transport, transportSettings);
        }

        static void ConfigureTransport(TransportExtensions<RabbitMQTransport> transport, TransportSettings transportSettings)
        {
            transport.UseConventionalRoutingTopology(QueueType.Classic);
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ApplyConnectionString(transportSettings.ConnectionString);
        }
    }
}