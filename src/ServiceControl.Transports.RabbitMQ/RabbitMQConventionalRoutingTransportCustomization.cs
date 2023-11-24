namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using NServiceBus;

    public abstract class RabbitMQConventionalRoutingTransportCustomization : TransportCustomization<RabbitMQTransport>
    {
        readonly QueueType queueType;

        protected RabbitMQConventionalRoutingTransportCustomization(QueueType queueType) => this.queueType = queueType;

        protected override void CustomizeTransportSpecificSendOnlyEndpointSettings(
            EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition,
            TransportSettings transportSettings)
        {
        }

        protected override void CustomizeTransportSpecificServiceControlEndpointSettings(
            EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition,
            TransportSettings transportSettings)
        {
        }

        protected override void CustomizeRawSendOnlyEndpoint(RabbitMQTransport transportDefinition,
            TransportSettings transportSettings)
        {
        }

        protected override void CustomizeForQueueIngestion(RabbitMQTransport transportDefinition,
            TransportSettings transportSettings)
        {
        }

        protected override void CustomizeTransportSpecificMonitoringEndpointSettings(
            EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition,
            TransportSettings transportSettings)
        {
        }

        protected override void CustomizeForReturnToSenderIngestion(RabbitMQTransport transportDefinition,
            TransportSettings transportSettings)
        {
        }

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();

        protected override RabbitMQTransport CreateTransport(TransportSettings transportSettings)
        {
            if (transportSettings.ConnectionString == null)
            {
                throw new InvalidOperationException("Connection string not configured");
            }

            var transport =
                new RabbitMQTransport(RoutingTopology.Conventional(queueType), transportSettings.ConnectionString)
                {
                    TransportTransactionMode = TransportTransactionMode.ReceiveOnly
                };
            return transport;
        }
    }
}