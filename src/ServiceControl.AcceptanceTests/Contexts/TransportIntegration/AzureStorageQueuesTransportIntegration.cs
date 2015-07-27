namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using System.Net;
    using Microsoft.WindowsAzure.Storage;
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
            var storageAccount = CloudStorageAccount.Parse(ConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();
            foreach (var queue in queueClient.ListQueues())
            {
                var localQueue = queue;
                IgnoreWebExceptionsForConcurrencyReasons(() => localQueue.Delete());
                Console.WriteLine("Deleted '{0}' queue", queue.Name);
            }
        }

        private void IgnoreWebExceptionsForConcurrencyReasons(Action action)
        {
            try
            {
                action();
            }
            catch (WebException)
            {
                // Concurrency exception
            }
        }
    }
}
