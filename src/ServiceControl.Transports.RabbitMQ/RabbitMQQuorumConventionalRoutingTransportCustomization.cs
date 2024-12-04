namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;

    public class RabbitMQQuorumConventionalRoutingTransportCustomization()
        : RabbitMQConventionalRoutingTransportCustomization(QueueType.Quorum);
}