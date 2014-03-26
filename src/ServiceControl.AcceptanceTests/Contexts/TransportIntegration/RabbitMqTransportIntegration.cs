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
        public string ConnectionString { get; set; }

        public void SetUp()
        {
        }

        public void TearDown()
        {
        }
    }
}
