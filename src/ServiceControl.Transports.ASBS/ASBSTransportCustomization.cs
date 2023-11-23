namespace ServiceControl.Transports.ASBS
{
    using NServiceBus;

    public class ASBSTransportCustomization : TransportCustomization<AzureServiceBusTransport>
    {
        protected override void CustomizeTransportSpecificMonitoringEndpointSettings(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
        }

        protected override void CustomizeForReturnToSenderIngestion(AzureServiceBusTransport transportDefinition, TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;
        }

        protected override void CustomizeTransportSpecificServiceControlEndpointSettings(
            EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition,
            TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;
        }

        protected override void CustomizeTransportSpecificSendOnlyEndpointSettings(EndpointConfiguration endpointConfiguration, AzureServiceBusTransport transportDefinition, TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        protected override void CustomizeRawSendOnlyEndpoint(AzureServiceBusTransport transportDefinition, TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
        }

        protected override void CustomizeForQueueIngestion(AzureServiceBusTransport transportDefinition, TransportSettings transportSettings)
        {
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
        }

        protected override AzureServiceBusTransport CreateTransport(TransportSettings transportSettings)
        {
            var connectionSettings = ConnectionStringParser.Parse(transportSettings.ConnectionString);
            var transport = connectionSettings.AuthenticationMethod.CreateTransportDefinition(connectionSettings);
            transport.UseWebSockets = connectionSettings.UseWebSockets;

            if (connectionSettings.TopicName != null)
            {
                transport.Topology = TopicTopology.Single(connectionSettings.TopicName);
            }

            transport.ConfigureNameShorteners();
            return transport;
        }
    }
}