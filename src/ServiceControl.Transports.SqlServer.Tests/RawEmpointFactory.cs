namespace ServiceControl.Transports.SqlServer.Tests
{
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using System.Threading.Tasks;
    using System;

    class RawEndpointFactory
    {
        public RawEndpointFactory(TransportSettings transportSettings, TransportCustomization transportCustomization)
        {
            this.transportSettings = transportSettings;
            //this.settings = settings;
            this.transportCustomization = transportCustomization;
        }

        public RawEndpointConfiguration CreateAuditIngestor(string name, Func<MessageContext, IDispatchMessages, Task> onMessage)
        {
            var config = RawEndpointConfiguration.Create(name, onMessage, $"{transportSettings.EndpointName}.Errors");
            //config.LimitMessageProcessingConcurrencyTo(0);

            transportCustomization.CustomizeForAuditIngestion(config, transportSettings);
            return config;
        }

        public RawEndpointConfiguration CreateFailedAuditsSender(string name)
        {
            var config = RawEndpointConfiguration.CreateSendOnly(name);

            transportCustomization.CustomizeRawSendOnlyEndpoint(config, transportSettings);
            return config;
        }

        //Settings.Settings settings;
        TransportCustomization transportCustomization;
        TransportSettings transportSettings;
    }
}
