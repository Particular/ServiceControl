namespace ServiceControl.Transports.LegacyAzureServiceBus
{
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    public class EndpointOrientedTopologyAzureServiceBusTransport : AzureServiceBusTransport
    {
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);
            var topology = extensions.UseEndpointOrientedTopology();
            topology.EnableMigrationToForwardingTopology();

            return base.Initialize(settings, connectionString);
        }
    }
}