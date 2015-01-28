namespace Particular.ServiceControl
{
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;

    class ConfigTransportConfig : IProvideConfiguration<TransportConfig>
    {
        public TransportConfig GetConfiguration()
        {
            return new TransportConfig
            {
                MaximumMessageThroughputPerSecond = 350,
                MaximumConcurrencyLevel = 10,
                MaxRetries = 3,
            };
        }
    }
}