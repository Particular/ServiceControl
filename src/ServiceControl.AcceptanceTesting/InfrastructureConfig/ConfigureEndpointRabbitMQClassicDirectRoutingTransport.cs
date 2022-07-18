namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using NServiceBus;
    using ServiceControl.Transports.RabbitMQ;
    using ServiceControlInstaller.Engine.Instances;

    public class ConfigureEndpointRabbitMQClassicDirectRoutingTransport : ConfigureEndpointRabbitMQDirectRoutingTransport
    {
        public ConfigureEndpointRabbitMQClassicDirectRoutingTransport()
            : base(QueueType.Classic)
        {

        }

        public override string Name => TransportNames.RabbitMQClassicDirectRoutingTopology;

        public override string TypeName => $"{typeof(RabbitMQClassicDirectRoutingTransportCustomization).AssemblyQualifiedName}";
    }
}
