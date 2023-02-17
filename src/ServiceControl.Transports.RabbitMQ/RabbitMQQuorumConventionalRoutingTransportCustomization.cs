namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using ServiceControl.Transports;

    public class RabbitMQQuorumConventionalRoutingTransportCustomization : RabbitMQConventionalRoutingTransportCustomization
    {
        public RabbitMQQuorumConventionalRoutingTransportCustomization()
            : base(QueueType.Quorum)
        {

        }
    }
}
