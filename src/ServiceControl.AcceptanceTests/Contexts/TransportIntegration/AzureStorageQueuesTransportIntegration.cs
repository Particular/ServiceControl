namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using NServiceBus;

    public class AzureStorageQueuesTransportIntegration : ITransportIntegration
    {
        public AzureStorageQueuesTransportIntegration()
        {
            ConnectionString = String.Empty; // empty on purpose
        }

        public string Name { get { return "AzureStorageQueues"; } }
        public Type Type { get { return typeof(AzureStorageQueueTransport); } }
        public string TypeName { get { return "NServiceBus.AzureStorageQueueTransport, NServiceBus.Azure.Transports.WindowsAzureStorageQueues"; } }
        public string ConnectionString { get; set; }

        public void OnEndpointShutdown(string endpointName)
        {
            
        }

        public void TearDown()
        {
            var storageAccount = CloudStorageAccount.Parse(ConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();
            var cloudQueues = queueClient.ListQueues();
            Parallel.ForEach(cloudQueues, queue =>
            {
                // NOTE: Do not delete the queue as it gets soft-deleted and cleaned up later
                // If another test tries to create a queue with the same name in the meantime 
                // you get a 409 - Conflict HttpResponse
                queue.Clear();
                Console.WriteLine("Cleared '{0}' queue", queue.Name);
            });
        }
    }
}
