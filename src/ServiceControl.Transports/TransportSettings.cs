namespace ServiceControl.Transports
{
    using NServiceBus;
    using NServiceBus.Settings;

    public class TransportSettings : SettingsHolder
    {
        public string ConnectionString { get; set; }

        public string EndpointName { get; set; }

        public int MaxConcurrency { get; set; }

        public bool RunCustomChecks { get; set; }

        public TransportTransactionMode PreferredTransactionMode { get; set; }
    }
}