namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Transports;

    public class RawEndpointFactory
    {
        Settings settings;
        TransportCustomization transportCustomization;
        private TransportSettings transportSettings;

        public RawEndpointFactory(Settings settings, TransportSettings transportSettings, TransportCustomization transportCustomization)
        {
            this.transportSettings = transportSettings;
            this.settings = settings;
            this.transportCustomization = transportCustomization;
        }

        public RawEndpointConfiguration CreateRawEndpointConfiguration(string name, Func<MessageContext, IDispatchMessages, Task> onMessage, TransportDefinition transportDefinition)
        {
            var config = RawEndpointConfiguration.Create(name, onMessage, $"{transportSettings.EndpointName}.errors");
            config.AutoCreateQueue();
            config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            transportCustomization.CustomizeRawEndpoint(config, transportSettings);
            return config;
        }
    }
}