namespace ServiceControl.Transports.Learning
{
    using System;
    using LearningTransport;
    using NServiceBus;

    public class LearningTransportCustomization : TransportCustomization<LearningTransport>
    {
        protected override void CustomizeTransportForAuditEndpoint(
            EndpointConfiguration endpointConfiguration, LearningTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForPrimaryEndpoint(
            EndpointConfiguration endpointConfiguration, LearningTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        protected override void CustomizeForQueueIngestion(LearningTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForMonitoringEndpoint(
            EndpointConfiguration endpointConfiguration, LearningTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeForReturnToSenderIngestion(LearningTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();

        protected override LearningTransport CreateTransport(TransportSettings transportSettings)
        {
            var transport = new LearningTransport
            {
                StorageDirectory = Environment.ExpandEnvironmentVariables(transportSettings.ConnectionString),
                RestrictPayloadSize = false,
                TransportTransactionMode = TransportTransactionMode.ReceiveOnly
            };


            return transport;
        }
    }
}