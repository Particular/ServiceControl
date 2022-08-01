namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;

    public class RabbitMQQuorumDirectRoutingTransportCustomization : RabbitMQDirectRoutingTransportCustomization
    {
        public RabbitMQQuorumDirectRoutingTransportCustomization() : base(QueueType.Quorum)
        {
        }
    }
}
