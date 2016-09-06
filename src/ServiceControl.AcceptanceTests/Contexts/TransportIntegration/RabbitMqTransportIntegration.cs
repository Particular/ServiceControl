namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using NServiceBus;
    using NServiceBus.Persistence;

    public class RabbitMqTransportIntegration : ITransportIntegration
    {
        public RabbitMqTransportIntegration()
        {
            ConnectionString = "host=localhost"; // Default connstr
        }

        public string Name => "RabbitMq";
        public Type Type => typeof(RabbitMQTransport);
        public string TypeName => "NServiceBus.RabbitMQTransport, NServiceBus.Transports.RabbitMQ";
        public string ConnectionString { get; set; }

        public void OnEndpointShutdown(string endpointName)
        {
        }

        public void TearDown()
        {
            
        }

        public void Setup()
        {
            
        }

        class CustomConfig : INeedInitialization
        {
            public void Customize(BusConfiguration configuration)
            {
                configuration.UsePersistence<InMemoryPersistence, StorageType.Outbox>();
                configuration.UseTransport<RabbitMQTransport>().DisableCallbackReceiver();
                configuration.EnableOutbox();
            }
        }
    }
}
