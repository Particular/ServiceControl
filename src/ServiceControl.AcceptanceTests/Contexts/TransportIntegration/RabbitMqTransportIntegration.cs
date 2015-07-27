namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using NServiceBus;

    public class RabbitMqTransportIntegration : ITransportIntegration
    {
        public RabbitMqTransportIntegration()
        {
            ConnectionString = "host=localhost"; // Default connstr
        }

        public string Name { get { return "RabbitMq"; } }
        public Type Type { get { return typeof(RabbitMQ); } }
        public string TypeName { get { return "NServiceBus.RabbitMQ, NServiceBus.Transports.RabbitMQ"; } }
        public string ConnectionString { get; set; }

        public void Cleanup()
        {
        }
    }
}
