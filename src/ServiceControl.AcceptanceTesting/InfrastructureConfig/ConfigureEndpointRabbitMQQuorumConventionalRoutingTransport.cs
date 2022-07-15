namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using NServiceBus;
    using ServiceControl.Transports.RabbitMQ;

    public class ConfigureEndpointRabbitMQQuorumConventionalRoutingTransport : ConfigureEndpointRabbitMQConventionalRoutingTransport
    {
        public ConfigureEndpointRabbitMQQuorumConventionalRoutingTransport()
            : base(QueueType.Quorum)
        {

        }

        public override string TypeName => $"{typeof(RabbitMQQuorumConventionalRoutingTransportCustomization).AssemblyQualifiedName}";

    }
}
