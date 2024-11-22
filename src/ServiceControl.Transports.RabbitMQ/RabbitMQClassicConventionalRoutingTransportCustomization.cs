namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;

    public class RabbitMQClassicConventionalRoutingTransportCustomization()
        : RabbitMQConventionalRoutingTransportCustomization(QueueType.Classic);
}