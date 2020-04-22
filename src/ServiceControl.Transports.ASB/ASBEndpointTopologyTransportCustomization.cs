namespace ServiceControl.Transports.ASB
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBEndpointTopologyTransportCustomization : TransportCustomization
    {
        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        public override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        static void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings, TransportTransactionMode transactionMode)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
            var topology = transport.UseEndpointOrientedTopology();
            topology.EnableMigrationToForwardingTopology();

            transport.ConfigureTransport(transportSettings, transactionMode);
        }

        static void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings, TransportTransactionMode transactionMode)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.UseEndpointOrientedTopology();
            transport.ApplyHacksForNsbRaw();

            transport.ConfigureTransport(transportSettings, transactionMode);
        }
    }
}