namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using NServiceBus;
    using NServiceBus.Raw;

    public abstract class RabbitMQConventionalRoutingTransportCustomization : TransportCustomization
    {
        readonly QueueType queueType;

        protected RabbitMQConventionalRoutingTransportCustomization(QueueType queueType)
        {
            this.queueType = queueType;
        }

        protected override void CustomizeTransportSpecificSendOnlyEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        protected override void CustomizeTransportSpecificServiceControlEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        protected override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        protected override void CustomizeForQueueIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        protected override void CustomizeTransportSpecificMonitoringEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
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
            if (transportSettings.ConnectionString == null)
            {
                throw new InvalidOperationException("Connection string not configured");
            }
            transport.UseConventionalRoutingTopology(queueType);
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ApplyConnectionString(transportSettings.ConnectionString);
        }
    }
}