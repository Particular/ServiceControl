namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using Microsoft.WindowsAzure.Storage;
    using NServiceBus;

    public class AzureStorageQueuesTransportIntegration : ITransportIntegration
    {
        public AzureStorageQueuesTransportIntegration()
        {
            ConnectionString = String.Empty; // empty on purpose
        }

        public string Name { get { return "AzureStorageQueues"; } }
        public Type Type { get { return typeof(AzureStorageQueue); } }
        public string TypeName { get { return "NServiceBus.AzureStorageQueue, NServiceBus.Azure.Transports.WindowsAzureStorageQueues"; } }
        public string ConnectionString { get; set; }

        public void OnEndpointShutdown()
        {
            
        }

        public void TearDown()
        {
            var storageAccount = CloudStorageAccount.Parse(ConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();
            foreach (var queue in queueClient.ListQueues())
            {
                // NOTE: Do not delete the queue as it gets soft-deleted and cleaned up later
                // If another test tries to create a queue with the same name in the meantime 
                // you get a 409 - Conflict HttpResponse
                queue.Clear();
                Console.WriteLine("Cleared '{0}' queue", queue.Name);
            }
        }
    }
}
