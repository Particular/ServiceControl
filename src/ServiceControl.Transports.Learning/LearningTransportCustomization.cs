namespace ServiceControl.Transports.Learning
{
    using System;
    using System.Linq;
    using LearningTransport;
    using NServiceBus;

    public class LearningTransportCustomization : TransportCustomization<LearningTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, LearningTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, LearningTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, LearningTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();

        protected override LearningTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var transport = new LearningTransport
            {
                StorageDirectory = Environment.ExpandEnvironmentVariables(transportSettings.ConnectionString),
                RestrictPayloadSize = false,
            };

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }
    }
}