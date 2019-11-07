namespace ServiceControl.Transports
{
    using NServiceBus;
    using NServiceBus.Raw;

    public abstract class TransportCustomization
    {
        public abstract void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings);

        public abstract void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings);

        //TODO: make abstract once all providers have been converted, or do we move default provide to MSMQ?
        public virtual IProvideQueueLengthNew CreateQueueLengthProvider()
        {
            return new DefaultQueueLengthProvider();
        }
    }
}