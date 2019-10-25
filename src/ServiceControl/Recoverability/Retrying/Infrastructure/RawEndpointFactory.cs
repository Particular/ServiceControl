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

        public RawEndpointConfiguration CreateRawEndpointConfiguration(string name, Func<MessageContext, IDispatchMessages, Task> onMessage)
        {
            var config = RawEndpointConfiguration.Create(name, onMessage, $"{transportSettings.EndpointName}.Errors");
            config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            transportCustomization.CustomizeRawEndpoint(config, transportSettings);
            return config;
        }

        public RawEndpointConfiguration CreateSendOnlyRawEndpointConfiguration(string name)
        {
            var config = RawEndpointConfiguration.CreateSendOnly(name);

            transportCustomization.CustomizeRawEndpoint(config, transportSettings);
            return config;
        }

        Settings settings;
        TransportCustomization transportCustomization;
        private TransportSettings transportSettings;
    }
}