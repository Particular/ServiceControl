namespace ServiceControl.Transports.ASB
{
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBForwardingTopologyTransportCustomization : TransportCustomization
    {
        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        protected override void CustomizeForQueueIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        protected override void CustomizeTransportSpecificMonitoringEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        protected override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        protected override void CustomizeTransportSpecificSendOnlyEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.ReceiveOnly);
        }

        protected override void CustomizeTransportSpecificServiceControlEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeEndpoint(endpointConfiguration, transportSettings, TransportTransactionMode.SendsAtomicWithReceive);
        }

        void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings, TransportTransactionMode transactionMode)
        {
#pragma warning disable 618
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
#pragma warning restore 618

            transport.UseForwardingTopology();
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
            transport.ConfigureTransport(transportSettings, transactionMode);
        }

        void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings, TransportTransactionMode transactionMode)
        {
#pragma warning disable 618
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
#pragma warning restore 618
            transport.ApplyHacksForNsbRaw();

            transport.UseForwardingTopology();
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
            transport.ConfigureTransport(transportSettings, transactionMode);
        }
    }
}