namespace ServiceControl.Transport
{
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;

    public abstract class TransportCustomization : INeedInitialization
    {
        protected abstract string Transport { get; }
        
        public void Customize(EndpointConfiguration configuration)
        {
            var settings = configuration.GetSettings();
            string selectedTransport;
            if (!settings.TryGet("ServiceControl.TransportType", out selectedTransport))
            {
                return;
            }

            if (selectedTransport != Transport)
            {
                return;
            }
            
            CustomizeTransport(configuration, settings.GetOrDefault<string>("ServiceControl.TransportConnectionString"));
        }

        protected abstract void CustomizeTransport(EndpointConfiguration configuration, string connectionString);
    }
}