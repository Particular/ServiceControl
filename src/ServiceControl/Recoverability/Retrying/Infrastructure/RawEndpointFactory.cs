namespace ServiceControl.Recoverability
{
    using NServiceBus.Raw;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class RawEndpointFactory
    {
        public RawEndpointFactory(TransportSettings transportSettings, TransportCustomization transportCustomization)
        {
            this.transportSettings = transportSettings;
            this.transportCustomization = transportCustomization;
        }

        public RawEndpointConfiguration CreateSendOnly(string name)
        {
            var config = RawEndpointConfiguration.CreateSendOnly(name);
            transportCustomization.CustomizeRawSendOnlyEndpoint(config, transportSettings);

            return config;
        }

        public RawEndpointConfiguration CreateFailedErrorsImporter(string name)
        {
            var config = RawEndpointConfiguration.CreateSendOnly(name);

            transportCustomization.CustomizeRawSendOnlyEndpoint(config, transportSettings);
            return config;
        }

        TransportCustomization transportCustomization;
        TransportSettings transportSettings;
    }
}