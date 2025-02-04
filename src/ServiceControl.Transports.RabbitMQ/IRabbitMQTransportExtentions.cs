namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;

    public interface IRabbitMQTransportExtensions
    {
        RabbitMQTransport GetTransport();
    }
}
