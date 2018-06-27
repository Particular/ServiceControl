namespace ServiceControl.Transport.RabbitMQ
{
    using NServiceBus;

    public class RabbitMQTransport : RabbitMQTransportCustomization
    {
        protected override void CustomizeTransport(EndpointConfiguration configuration, string connectionString)
        {
            var transport = configuration.UseTransport<NServiceBus.RabbitMQTransport>();
            transport.UseConventionalRoutingTopology();
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            transport.ConnectionString(connectionString);
        }
    }
}