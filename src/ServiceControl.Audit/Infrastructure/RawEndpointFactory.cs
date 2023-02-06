namespace ServiceControl.Audit.Infrastructure
{
    using NServiceBus.Raw;
    using Transports;

    class RawEndpointFactory
    {
        public RawEndpointFactory(Settings.Settings settings, TransportSettings transportSettings, TransportCustomization transportCustomization)
        {
            this.transportSettings = transportSettings;
            this.settings = settings;
            this.transportCustomization = transportCustomization;
        }

#pragma warning disable CA1822 // Mark members as static
        public IQueueIngestorFactory CreateQueueIngestorFactory()
#pragma warning restore CA1822 // Mark members as static
        {
            //var config = RawEndpointConfiguration.Create(name, onMessage, $"{transportSettings.EndpointName}.Errors");
            //config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            //transportCustomization.CustomizeForAuditIngestion(config, transportSettings);
            //return config;

            return null;
        }

        public RawEndpointConfiguration CreateFailedAuditsSender(string name)
        {
            var config = RawEndpointConfiguration.CreateSendOnly(name);

            transportCustomization.CustomizeRawSendOnlyEndpoint(config, transportSettings);
            return config;
        }

#pragma warning disable IDE0052 // Remove unread private members
        Settings.Settings settings;
#pragma warning restore IDE0052 // Remove unread private members
        TransportCustomization transportCustomization;
        TransportSettings transportSettings;
    }
}