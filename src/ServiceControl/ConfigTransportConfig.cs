namespace Particular.ServiceControl
{
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using ServiceBus.Management.Infrastructure.Settings;

    class ConfigTransportConfig : IProvideConfiguration<TransportConfig>
    {
        public TransportConfig GetConfiguration()
        {
            return new TransportConfig
            {
               // MaximumMessageThroughputPerSecond = Settings.MaximumMessageThroughputPerSecond,
                MaximumConcurrencyLevel = 1,
                MaxRetries = 3,
            };
        }
    }
}