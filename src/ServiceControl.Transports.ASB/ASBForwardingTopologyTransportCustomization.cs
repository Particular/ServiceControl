namespace ServiceControl.Transports.ASB
{
    using System;
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBForwardingTopologyTransportCustomization : TransportCustomizationBase
    {
        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            throw new NotImplementedException();
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            throw new NotImplementedException();
        }

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

        void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings, TransportTransactionMode transactionMode)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();

            transport.UseForwardingTopology();
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
            transport.ConfigureTransport(transportSettings, transactionMode);
        }

        void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings, TransportTransactionMode transactionMode)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();
            transport.ApplyHacksForNsbRaw();

            transport.UseForwardingTopology();
            transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
            transport.ConfigureTransport(transportSettings, transactionMode);
        }
    }
}