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

                MaximumConcurrencyLevel = 10,
                MaxRetries = 3,
            };
        }
    }
}