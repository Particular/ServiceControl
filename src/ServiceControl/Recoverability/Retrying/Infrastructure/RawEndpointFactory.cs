namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class RawEndpointFactory
    {
        public RawEndpointFactory(Settings settings, TransportSettings transportSettings, TransportCustomization transportCustomization)
        {
            this.transportSettings = transportSettings;
            this.settings = settings;
            this.transportCustomization = transportCustomization;
        }

        public RawEndpointConfiguration CreateErrorIngestor(string name, Func<MessageContext, IDispatchMessages, Task> onMessage)
        {
            var config = RawEndpointConfiguration.Create(name, onMessage, $"{transportSettings.EndpointName}.Errors");
            config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            transportCustomization.CustomizeForErrorIngestion(config, transportSettings);
            return config;
        }

        public RawEndpointConfiguration CreateReturnToSenderDequeuer(string name, Func<MessageContext, IDispatchMessages, Task> onMessage)
        {
            var config = RawEndpointConfiguration.Create(name, onMessage, $"{transportSettings.EndpointName}.Errors");
            config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            transportCustomization.CustomizeForReturnToSenderIngestion(config, transportSettings);
            return config;
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

        Settings settings;
        TransportCustomization transportCustomization;
        TransportSettings transportSettings;
    }
}