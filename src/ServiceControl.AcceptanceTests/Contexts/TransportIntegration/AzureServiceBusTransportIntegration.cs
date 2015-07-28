namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using System.Linq;
    using Microsoft.ServiceBus;
    using NServiceBus;

    public class AzureServiceBusTransportIntegration : ITransportIntegration
    {
        public AzureServiceBusTransportIntegration()
        {
            ConnectionString = String.Empty; // empty on purpose
        }

        public string Name
        {
            get { return "AzureServiceBus"; }
        }

        public Type Type
        {
            get { return typeof(AzureServiceBus); }
        }

        public string TypeName
        {
            get { return "NServiceBus.AzureServiceBus, NServiceBus.Azure.Transports.WindowsAzureServiceBus"; }
        }

        public string ConnectionString { get; set; }

        public void OnEndpointShutdown()
        {
        }

        public void TearDown()
        {
            var namespaceManager = NamespaceManager.CreateFromConnectionString(ConnectionString);

            var topics = namespaceManager.GetTopics().ToList();
            foreach (var topic in topics)
            {
                var subscriptions = namespaceManager.GetSubscriptions(topic.Path);
                foreach (var subscription in subscriptions)
                {
                    var topic1 = topic;
                    var subscription1 = subscription;

                    namespaceManager.DeleteSubscription(topic1.Path, subscription1.Name);
                    Console.WriteLine("Deleted subscription '{0}' for topic {1}", subscription1.Name, topic1.Path);
                }

                var topic2 = topic;
                namespaceManager.DeleteTopic(topic2.Path);
                Console.WriteLine("Deleted '{0}' topic", topic2.Path);
            }

            var queues = namespaceManager.GetQueues();
            foreach (var queue in queues)
            {
                var queue1 = queue;
                namespaceManager.DeleteQueue(queue1.Path);
                Console.WriteLine("Deleted '{0}' queue", queue1.Path);
            }
        }
    }
}
