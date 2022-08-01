namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using NServiceBus;
    using ServiceControl.Transports.RabbitMQ;
    using ServiceControlInstaller.Engine.Instances;

    public class ConfigureEndpointRabbitMQClassicConventionalRoutingTransport : ConfigureEndpointRabbitMQConventionalRoutingTransport
    {
        public ConfigureEndpointRabbitMQClassicConventionalRoutingTransport()
            : base(QueueType.Classic)
        {

        }

        public override string Name => TransportNames.RabbitMQClassicConventionalRoutingTopology;

        public override string TypeName => $"{typeof(RabbitMQClassicConventionalRoutingTransportCustomization).AssemblyQualifiedName}";

    }
}
