namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using NServiceBus;

    public class AzureStorageQueuesTransportIntegration
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

        // TODO: This
        //class MyConfig : IProvideConfiguration<AzureQueueConfig>
        //{
        //    public AzureQueueConfig GetConfiguration()
        //    {
        //        return new AzureQueueConfig
        //        {
        //            BatchSize = 5
        //        };
        //    }
        //}
    }
}
