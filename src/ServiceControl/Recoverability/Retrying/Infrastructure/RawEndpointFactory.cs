namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;

    public class RawEndpointFactory
    {
        Settings settings;

        public RawEndpointFactory(Settings settings)
        {
            this.settings = settings;
        }

        public RawEndpointConfiguration CreateRawEndpointConfiguration(string name, Func<MessageContext, IDispatchMessages, Task> onMessage, TransportDefinition transportDefinition)
        {
            var config = RawEndpointConfiguration.Create(name, onMessage, $"{settings.ServiceName}.errors");
            config.AutoCreateQueue();
            config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            // only partially works because we are missing important things from the config like the transport connection string
            // plus this might be dangerous because we suddenly share things we should never have been sharing
            config.Settings.Set<TransportDefinition>(transportDefinition);
            return config;
        }
    }
}