namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;
    using NServiceBus.Raw;

    public abstract class RabbitMQDirectRoutingTransportCustomization : TransportCustomization
    {
        readonly QueueType queueType;

        protected RabbitMQDirectRoutingTransportCustomization(QueueType queueType)
        {
            this.queueType = queueType;
        }

        public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        public override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        public override void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        public override void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        static void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings, QueueType queueType)
        {
            var transport = endpointConfig.UseTransport<RabbitMQTransport>();
            ConfigureTransport(transport, transportSettings, queueType);
        }

        static void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings, QueueType queueType)
        {
            var transport = endpointConfig.UseTransport<RabbitMQTransport>();
            ConfigureTransport(transport, transportSettings, queueType);
        }

        static void ConfigureTransport(TransportExtensions<RabbitMQTransport> transport, TransportSettings transportSettings, QueueType queueType)
        {
            transport.UseDirectRoutingTopology(queueType, routingKeyConvention: type => type.FullName.Replace(".", "-"));
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ApplyConnectionString(transportSettings.ConnectionString);
        }
    }
}