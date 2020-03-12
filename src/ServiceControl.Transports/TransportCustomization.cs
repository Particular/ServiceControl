namespace ServiceControl.Transports
{
    using NServiceBus;
    using NServiceBus.Raw;

    public abstract class TransportCustomization
    {
        public abstract void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        public abstract IProvideQueueLength CreateQueueLengthProvider();

        public virtual IConsumeBatches CreateBatchConsumer()
        {
            return null;
        }
    }
}