namespace ServiceControl.Infrastructure.Transport
{
    using NServiceBus;
    using NServiceBus.Raw;

    public abstract class TransportCustomization
    {
        public abstract void CustomizeEndpoint(EndpointConfiguration endpointConfig, string connectionString);
        public abstract void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, string connectionString);
    }
}
