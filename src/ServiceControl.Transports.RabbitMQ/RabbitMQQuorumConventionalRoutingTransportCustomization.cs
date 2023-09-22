namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;

    public class RabbitMQQuorumConventionalRoutingTransportCustomization : RabbitMQConventionalRoutingTransportCustomization
    {
        public RabbitMQQuorumConventionalRoutingTransportCustomization()
            : base(QueueType.Quorum)
        {

        }
    }
}
