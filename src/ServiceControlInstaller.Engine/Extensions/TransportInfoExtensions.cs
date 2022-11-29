namespace ServiceControl.Engine.Extensions
{
    using ServiceControlInstaller.Engine.Instances;

    public static class TransportInfoExtensions
    {
        public static bool IsLatestRabbitMQTransport(this TransportInfo transport)
        {
            return transport.Name is TransportNames.RabbitMQClassicConventionalRoutingTopology or
                   TransportNames.RabbitMQQuorumConventionalRoutingTopology or
                   TransportNames.RabbitMQClassicDirectRoutingTopology or
                   TransportNames.RabbitMQQuorumDirectRoutingTopology;
        }

        public static bool IsOldRabbitMQTransport(this TransportInfo transport)
        {
            return transport.Name is TransportNames.RabbitMQConventionalRoutingTopologyDeprecated or
                   TransportNames.RabbitMQDirectRoutingTopologyDeprecated;
        }
    }
}
