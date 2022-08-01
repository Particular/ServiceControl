namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;

    public class RabbitMQClassicConventionalRoutingTransportCustomization : RabbitMQConventionalRoutingTransportCustomization
    {
        public RabbitMQClassicConventionalRoutingTransportCustomization()
            : base(QueueType.Classic)
        {

        }
    }
}
