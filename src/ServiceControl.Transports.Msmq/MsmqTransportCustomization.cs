namespace ServiceControl.Transports.Msmq
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using NServiceBus.Transport;

    public class MsmqTransportCustomization : TransportCustomization
    {
        public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = CustomizeRawEndpoint(endpointConfiguration, TransportTransactionMode.ReceiveOnly);
            transport.IgnoreIncomingTimeToBeReceivedHeaders();
        }

        public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = CustomizeRawEndpoint(endpointConfiguration, TransportTransactionMode.ReceiveOnly);
            transport.IgnoreIncomingTimeToBeReceivedHeaders();
        }

        public override void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = CustomizeRawEndpoint(endpointConfiguration, TransportTransactionMode.SendsAtomicWithReceive);
            transport.IgnoreIncomingTimeToBeReceivedHeaders();
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        static void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportTransactionMode transportTransactionMode)
        {
            var transport = endpointConfig.UseTransport<MsmqTransport>();
            transport.Transactions(transportTransactionMode);
        }

        static TransportExtensions<MsmqTransport> CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportTransactionMode transportTransactionMode)
        {
            var transport = endpointConfig.UseTransport<MsmqTransport>();
            transport.Transactions(transportTransactionMode);
            return transport;
        }

        public override Task<IQueueIngestor> InitializeQueueIngestor(string queueName, TransportSettings transportSettings, Func<MessageContext, Task> onMessage, IErrorHandlingPolicy onError, Func<string, Exception, Task> onCriticalError) => throw new NotImplementedException();
    }
}