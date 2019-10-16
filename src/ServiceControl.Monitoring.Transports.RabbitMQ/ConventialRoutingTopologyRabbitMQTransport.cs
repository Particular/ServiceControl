namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    public class ConventialRoutingTopologyRabbitMQTransport : RabbitMQTransport
    {
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            var extensions = new TransportExtensions<RabbitMQTransport>(settings);

            extensions.UseConventionalRoutingTopology();
            extensions.ApplyConnectionString(connectionString);

            return base.Initialize(settings, connectionString);
        }
    }
}
