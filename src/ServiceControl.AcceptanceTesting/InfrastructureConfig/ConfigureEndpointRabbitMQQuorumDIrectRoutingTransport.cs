namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using NServiceBus;
    using ServiceControl.Transports.RabbitMQ;
    using ServiceControlInstaller.Engine.Instances;

    public class ConfigureEndpointRabbitMQQuorumDirectRoutingTransport : ConfigureEndpointRabbitMQDirectRoutingTransport
    {
        public ConfigureEndpointRabbitMQQuorumDirectRoutingTransport()
               : base(QueueType.Quorum)
        {

        }

        public override string Name => TransportNames.RabbitMQQuorumDirectRoutingTopology;

        public override string TypeName => $"{typeof(RabbitMQQuorumDirectRoutingTransportCustomization).AssemblyQualifiedName}";
    }
}
