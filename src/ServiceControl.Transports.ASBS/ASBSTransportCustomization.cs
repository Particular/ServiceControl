namespace ServiceControl.Transports.ASBS
{
    using NServiceBus;
    using NServiceBus.Raw;
    using NServiceBus.Transport;

    public class ASBSTransportCustomization : TransportCustomization
    {
        protected override void CustomizeForQueueIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        protected override void CustomizeTransportSpecificMonitoringEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        protected override void CustomizeTransportSpecificServiceControlEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        protected override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        protected override void CustomizeTransportSpecificSendOnlyEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            CustomizeEndpoint(transport, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        protected override void CustomizeRawSendOnlyEndpoint(TransportDefinition transportDefinition, TransportSettings transportSettings)
        {
            ((AzureServiceBusTransport)transportDefinition).TransportTransactionMode =
                TransportTransactionMode.ReceiveOnly;
        }

        protected override void CustomizeForQueueIngestion(TransportDefinition transportDefinition, TransportSettings transportSettings)
        {
            throw new System.NotImplementedException();
        }

        protected override TransportDefinition CreateTransport(TransportSettings transportSettings)
        {
            var connectionSettings = ConnectionStringParser.Parse(transportSettings.ConnectionString);
            var asb = connectionSettings.AuthenticationMethod.ConfigureConnection(transport);
            if (connectionSettings.TopicName != null)
            {
                asb.TopicName(connectionSettings.TopicName);
            }

            if (connectionSettings.UseWebSockets)
            {
                asb.UseWebSockets();
            }

            transport.ConfigureNameShorteners();
            return asb;
        }

        void CustomizeEndpoint(
            TransportExtensions<AzureServiceBusTransport> transport,
            TransportSettings transportSettings,
            TransportTransactionMode transportTransactionMode)
        {
            ((AzureServiceBusTransport)transportDefinition).TransportTransactionMode =
                TransportTransactionMode.ReceiveOnly;
        }
    }
}