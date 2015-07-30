namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
    using NServiceBus;

    public class AzureServiceBusTransportIntegration : ITransportIntegration
    {
        const int BATCH_SIZE_FOR_CLEARING = 1000;
        const int SECONDS_TO_WAIT_FOR_BATCH = 10;

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

            var topics = namespaceManager.GetTopics();
            Parallel.ForEach(topics, topic =>
            {
                var subscriptions = namespaceManager.GetSubscriptions(topic.Path);

                Task.WaitAll(subscriptions.Select(x => ClearSubscription(topic.Path, x.Name)).ToArray());
            });

            Task.WaitAll(namespaceManager.GetQueues().Select(q => ClearQueue(q.Path)).ToArray());
        }

        async Task ClearQueue(string queuePath)
        {
	        var client = QueueClient.CreateFromConnectionString(ConnectionString, queuePath, ReceiveMode.ReceiveAndDelete);
	        IEnumerable<BrokeredMessage> messages;
	        do
	        {
	    	    messages = await client.ReceiveBatchAsync(BATCH_SIZE_FOR_CLEARING, TimeSpan.FromSeconds(SECONDS_TO_WAIT_FOR_BATCH));
	        } while(messages.Any());
		
	        Console.WriteLine("Cleared '{0}' queue", queuePath);
        }

        async Task ClearSubscription(string topicPath, string name)
        {
            var client = SubscriptionClient.CreateFromConnectionString(ConnectionString, topicPath, name, ReceiveMode.ReceiveAndDelete);
            IEnumerable<BrokeredMessage> messages;
            do
            {
                messages = await client.ReceiveBatchAsync(BATCH_SIZE_FOR_CLEARING, TimeSpan.FromSeconds(SECONDS_TO_WAIT_FOR_BATCH));
            } while (messages.Any());

            Console.WriteLine("Cleared '{0}->{1}' subscription", topicPath, name);
        }
    }

}
