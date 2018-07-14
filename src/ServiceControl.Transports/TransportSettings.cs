namespace ServiceControl.Transports
{
    using NServiceBus.Settings;
    
    public class TransportSettings : SettingsHolder
    {
        public string ConnectionString { get; set; }
        
        public bool EnableDTC { get; set; }

        public string EndpointName { get; set; }
    }
}