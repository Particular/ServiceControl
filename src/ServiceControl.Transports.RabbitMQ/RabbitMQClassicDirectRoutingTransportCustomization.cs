namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;

    public class RabbitMQClassicDirectRoutingTransportCustomization()
        : RabbitMQDirectRoutingTransportCustomization(QueueType.Classic);
}