namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using NServiceBus;

    public class AzureStorageQueuesTransportIntegration : ITransportIntegration
    {
        public AzureStorageQueuesTransportIntegration()
        {
            ConnectionString = ""; // empty on purpose
        }

        public string Name { get { return "AzureStorageQueues"; } }
        public Type Type { get { return typeof(AzureStorageQueue); } }
        public string TypeName { get { return "NServiceBus.AzureStorageQueue, NServiceBus.Azure.Transports.WindowsAzureStorageQueues"; } }
        public string ConnectionString { get; set; }

        public void Cleanup(ITransportIntegration transport)
        {

        }
    }
}
