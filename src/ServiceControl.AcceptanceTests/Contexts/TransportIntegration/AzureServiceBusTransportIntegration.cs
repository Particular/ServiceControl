namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using Microsoft.ServiceBus;
    using NServiceBus;

    public class AzureServiceBusTransportIntegration : ITransportIntegration
    {
        public AzureServiceBusTransportIntegration()
        {
            ConnectionString = ""; // empty on purpose
        }

        public string Name { get { return "AzureServiceBus"; } }
        public Type Type { get { return typeof(AzureServiceBus); } }
        public string TypeName { get { return "NServiceBus.AzureServiceBus, NServiceBus.Azure.Transports.WindowsAzureServiceBus"; } }
        public string ConnectionString { get; set; }

        public void Cleanup(ITransportIntegration transport)
        {
            var namespaceManager = NamespaceManager.CreateFromConnectionString(ConnectionString);

            var topics = namespaceManager.GetTopics();
            foreach (var topic in topics)
            {
                var subscriptions = namespaceManager.GetSubscriptions(topic.Path);
                foreach (var subscription in subscriptions)
                {
                    namespaceManager.DeleteSubscription(topic.Path, subscription.Name);
                    Console.WriteLine("Deleted subscription '{0}' for topic {1}", subscription.Name, topic.Path);
                }

                namespaceManager.DeleteTopic(topic.Path);
                Console.WriteLine("Deleted '{0}' topic", topic.Path);
            }

            var queues = namespaceManager.GetQueues();
            foreach (var queue in queues)
            {
                namespaceManager.DeleteQueue(queue.Path);
                Console.WriteLine("Deleted '{0}' queue", queue.Path);
            }

        }
    }
}
