namespace Particular.ServiceControl
{
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Settings;
    using ServiceBus.Management.Infrastructure.Settings;

    class ConfigTransportConfig : IProvideConfiguration<TransportConfig>
    {
        private Settings settings;

        public ConfigTransportConfig(ReadOnlySettings settings)
        {
            this.settings = settings.Get<Settings>("ServiceControl.Settings");
        }

        public TransportConfig GetConfiguration()
        {
            return new TransportConfig
            {
                MaximumConcurrencyLevel = settings.MaximumConcurrencyLevel,
                MaxRetries = 3,
            };
        }
    }
}