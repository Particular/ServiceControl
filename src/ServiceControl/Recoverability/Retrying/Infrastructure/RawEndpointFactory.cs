namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class RawEndpointFactory
    {
        public RawEndpointFactory(Settings settings, TransportSettings transportSettings, ITransportCustomization transportCustomization)
        {
            this.transportSettings = transportSettings;
            this.settings = settings;
            this.transportCustomization = transportCustomization;
        }

        public RawEndpointConfiguration CreateReturnToSenderDequeuer(string name, Func<MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage)
        {
            var config = transportCustomization.CreateRawEndpointForReturnToSenderIngestion(name, onMessage, transportSettings);
            config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);
            return config;
        }

        Settings settings;
        ITransportCustomization transportCustomization;
        TransportSettings transportSettings;
    }
}