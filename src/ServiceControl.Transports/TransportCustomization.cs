namespace ServiceControl.Transports
{
    using NServiceBus;
    using NServiceBus.Raw;

    public abstract class TransportCustomization
    {
        public abstract void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings);
        public abstract void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings);
    }
}
