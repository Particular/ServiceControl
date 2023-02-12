namespace ServiceControl.Transports.RabbitMQ
{
    using System.Threading.Tasks;
    using System;
    using NServiceBus;
    using NServiceBus.Raw;
    using NServiceBus.Transport;

    public abstract class RabbitMQConventionalRoutingTransportCustomization : TransportCustomization
    {
        readonly QueueType queueType;

        protected RabbitMQConventionalRoutingTransportCustomization(QueueType queueType)
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

        public override void CustomizeForQueueIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, queueType);
        }

        public override void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
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
            transport.UseConventionalRoutingTopology(queueType);
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ApplyConnectionString(transportSettings.ConnectionString);
        }

        public override Task<IQueueIngestor> InitializeQueueIngestor(string queueName, TransportSettings transportSettings, Func<MessageContext, Task> onMessage, IErrorHandlingPolicy onError, Func<string, Exception, Task> onCriticalError) => throw new NotImplementedException();
    }
}