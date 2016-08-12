namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using NServiceBus;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;

    public class AzureStorageQueuesTransportIntegration : ITransportIntegration
    {
        public AzureStorageQueuesTransportIntegration()
        {
            ConnectionString = String.Empty; // empty on purpose
        }

        public string Name => "AzureStorageQueues";
        public Type Type => typeof(AzureStorageQueueTransport);
        public string TypeName => "NServiceBus.AzureStorageQueueTransport, NServiceBus.Azure.Transports.WindowsAzureStorageQueues";
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

        class MyConfig : IProvideConfiguration<AzureQueueConfig>
        {
            public AzureQueueConfig GetConfiguration()
            {
                return new AzureQueueConfig
                {
                    BatchSize = 5
                };
            }
        }
    }
}
