namespace ServiceControl.Transports.Msmq
{
    using NServiceBus;

    public class MsmqTransportCustomization : TransportCustomization<MsmqTransport>
    {
        protected override void CustomizeTransportForAuditEndpoint(
            EndpointConfiguration endpointConfiguration, MsmqTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForPrimaryEndpoint(
            EndpointConfiguration endpointConfiguration, MsmqTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        protected override void CustomizeForQueueIngestion(MsmqTransport transportDefinition,
            TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
            transportDefinition.IgnoreIncomingTimeToBeReceivedHeaders = true;
        }

        protected override void CustomizeTransportForMonitoringEndpoint(
            EndpointConfiguration endpointConfiguration, MsmqTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeForReturnToSenderIngestion(MsmqTransport transportDefinition,
            TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;
            transportDefinition.IgnoreIncomingTimeToBeReceivedHeaders = true;
        }

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();

        protected override MsmqTransport CreateTransport(TransportSettings transportSettings)
        {
            var transport = new MsmqTransport { TransportTransactionMode = TransportTransactionMode.ReceiveOnly };
            return transport;
        }
    }
}