namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.Transport;

    public class RawEndpointFactory
    {
        Settings settings;
        TransportCustomization transportCustomization;

        public RawEndpointFactory(Settings settings, TransportCustomization transportCustomization)
        {
            this.settings = settings;
            this.transportCustomization = transportCustomization;
        }

        public RawEndpointConfiguration CreateRawEndpointConfiguration(string name, Func<MessageContext, IDispatchMessages, Task> onMessage, TransportDefinition transportDefinition)
        {
            var config = RawEndpointConfiguration.Create(name, onMessage, $"{settings.ServiceName}.errors");
            config.AutoCreateQueue();
            config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            transportCustomization.CustomizeRawEndpoint(config, settings.TransportConnectionString);
            return config;
        }
    }
}