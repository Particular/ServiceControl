namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using System.Linq;
    using NServiceBus;

    public abstract class RabbitMQConventionalRoutingTransportCustomization : TransportCustomization<RabbitMQTransport>
    {
        readonly QueueType queueType;

        protected RabbitMQConventionalRoutingTransportCustomization(QueueType queueType) => this.queueType = queueType;

        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, RabbitMQTransport transportDefinition, TransportSettings transportSettings) { }

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();

        protected override RabbitMQTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            if (transportSettings.ConnectionString == null)
            {
                throw new InvalidOperationException("Connection string not configured");
            }

            var transport = new RabbitMQTransport(RoutingTopology.Conventional(queueType), transportSettings.ConnectionString);
            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;
            //transport.NetworkRecoveryInterval = TimeSpan.FromMilliseconds(100);

            return transport;
        }
    }
}