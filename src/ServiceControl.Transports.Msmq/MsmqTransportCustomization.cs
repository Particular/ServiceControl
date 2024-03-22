namespace ServiceControl.Transports.Msmq
{
    using System;
    using System.Linq;
    using NServiceBus;

    public class MsmqTransportCustomization : TransportCustomization<MsmqTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, MsmqTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, MsmqTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, MsmqTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();

        public override Type ThroughputQueryProvider => null;

        protected override MsmqTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var transport = new MsmqTransport
            {
                // By default this setting is set to make sure MSMQ doesn't discard messages that might get ingested
                // or moved from staging to the destination queue. Should one of the regular endpoints ever want to use
                // TTBR they would need to set this flag to false in the corresponding Customize methods
                // At the time of this comment we couldn't find any usage of TTBR.
                IgnoreIncomingTimeToBeReceivedHeaders = true
            };

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }
    }
}