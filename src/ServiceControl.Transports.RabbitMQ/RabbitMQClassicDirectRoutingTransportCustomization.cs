namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;

    public class RabbitMQClassicDirectRoutingTransportCustomization : RabbitMQDirectRoutingTransportCustomization
    {
        public RabbitMQClassicDirectRoutingTransportCustomization() : base(QueueType.Classic)
        {
        }
    }
}
