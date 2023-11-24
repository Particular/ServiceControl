﻿namespace ServiceControl.Transports.Msmq
{
    using NServiceBus;

    public class MsmqTransportCustomization : TransportCustomization<MsmqTransport>
    {
        protected override void CustomizeTransportSpecificSendOnlyEndpointSettings(
            EndpointConfiguration endpointConfiguration, MsmqTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportSpecificServiceControlEndpointSettings(
            EndpointConfiguration endpointConfiguration, MsmqTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        protected override void CustomizeRawSendOnlyEndpoint(MsmqTransport transportDefinition,
            TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeForQueueIngestion(MsmqTransport transportDefinition,
            TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
            transportDefinition.IgnoreIncomingTimeToBeReceivedHeaders = true;
        }

        protected override void CustomizeTransportSpecificMonitoringEndpointSettings(
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
            var transport = new MsmqTransport();
            return transport;
        }
    }
}