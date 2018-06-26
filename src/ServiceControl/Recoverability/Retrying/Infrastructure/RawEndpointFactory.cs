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

        public RawEndpointConfiguration CreateRawEndpointConfiguration(string name, Func<MessageContext, IDispatchMessages, Task> onMessage, TransportTransactionMode transactionMode)
        {
            var config = RawEndpointConfiguration.Create(name, onMessage, $"{settings.ServiceName}.Errors");
            config.AutoCreateQueue();
            config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);
            var transport = config.UseTransport(settings.TransportType);
            var s = settings.TransportConnectionString;
            if (s != null)
            {
                transport.ConnectionString(s);
            }
            transport.Transactions(transactionMode);
            return config;
        }
    }
}