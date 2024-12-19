namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;

    public class RabbitMQQuorumDirectRoutingTransportCustomization()
        : RabbitMQDirectRoutingTransportCustomization(QueueType.Quorum);
}