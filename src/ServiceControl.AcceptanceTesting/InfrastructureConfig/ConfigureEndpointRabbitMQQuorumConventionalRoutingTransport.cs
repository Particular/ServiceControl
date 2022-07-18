namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using NServiceBus;
    using ServiceControl.Transports.RabbitMQ;
    using ServiceControlInstaller.Engine.Instances;

    public class ConfigureEndpointRabbitMQQuorumConventionalRoutingTransport : ConfigureEndpointRabbitMQConventionalRoutingTransport
    {
        public ConfigureEndpointRabbitMQQuorumConventionalRoutingTransport()
            : base(QueueType.Quorum)
        {

        }

        public override string Name => TransportNames.RabbitMQQuorumConventionalRoutingTopology;

        public override string TypeName => $"{typeof(RabbitMQQuorumConventionalRoutingTransportCustomization).AssemblyQualifiedName}";


    }
}
