namespace ServiceControl.Transports.LegacyAzureServiceBus
{
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    public class ForwardingTopologyAzureServiceBusTransport : AzureServiceBusTransport
    {
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);
            extensions.UseForwardingTopology();

            return base.Initialize(settings, connectionString);
        }
    }
}