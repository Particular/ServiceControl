namespace ServiceControl.Config.Extensions
{
    using ServiceControlInstaller.Engine.Instances;

    static class TransportInfoExtensions
    {
        public static bool IsLatestRabbitMQTransport(this TransportInfo transport)
        {
            return transport.Name == TransportNames.RabbitMQClassicConventionalRoutingTopology ||
                   transport.Name == TransportNames.RabbitMQQuorumConventionalRoutingTopology ||
                   transport.Name == TransportNames.RabbitMQClassicDirectRoutingTopology ||
                   transport.Name == TransportNames.RabbitMQQuorumDirectRoutingTopology;
        }

        public static bool IsOldRabbitMQTransport(this TransportInfo transport)
        {
            return transport.Name == TransportNames.RabbitMQConventionalRoutingTopologyDeprecated ||
                   transport.Name == TransportNames.RabbitMQDirectRoutingTopologyDeprecated;
        }
    }
}
