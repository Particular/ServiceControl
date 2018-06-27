namespace ServiceControl.Transport
{
    public abstract class RabbitMQTransportCustomization : TransportCustomization
    {
        protected sealed override string Transport { get; } = "NServiceBus.RabbitMQTransport, NServiceBus.Transport.RabbitMQ";
    }
}