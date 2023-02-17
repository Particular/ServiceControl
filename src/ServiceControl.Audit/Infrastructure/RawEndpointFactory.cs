namespace ServiceControl.Audit.Infrastructure
{
    using NServiceBus.Raw;
    using Transports;

    class RawEndpointFactory
    {
        public RawEndpointFactory(TransportSettings transportSettings, TransportCustomization transportCustomization)
        {
            this.transportSettings = transportSettings;
            this.transportCustomization = transportCustomization;
        }

        public RawEndpointConfiguration CreateRawSendOnlyEndpoint(string name)
        {
            var config = RawEndpointConfiguration.CreateSendOnly(name);

            transportCustomization.CustomizeRawSendOnlyEndpoint(config, transportSettings);
            return config;
        }

        TransportCustomization transportCustomization;
        TransportSettings transportSettings;
    }
}