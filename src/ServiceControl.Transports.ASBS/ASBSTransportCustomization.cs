namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Linq;
    using NServiceBus;

    public class ASBSTransportCustomization : TransportCustomization<AzureServiceBusTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        public override IProvideQueueLength CreateQueueLengthProvider() => new QueueLengthProvider();
        public override Type ThroughputQueryProvider => typeof(AzureQuery);

        protected override AzureServiceBusTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var connectionSettings = ConnectionStringParser.Parse(transportSettings.ConnectionString);
            var transport = connectionSettings.AuthenticationMethod.CreateTransportDefinition(connectionSettings);
            transport.UseWebSockets = connectionSettings.UseWebSockets;

            if (connectionSettings.TopicName != null)
            {
                transport.Topology = TopicTopology.Single(connectionSettings.TopicName);
            }

            transport.EnablePartitioning = connectionSettings.EnablePartitioning;

            transport.ConfigureNameShorteners();

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }
    }
}