namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using NServiceBus;
    using NServiceBus.Azure.Transports.WindowsAzureServiceBus;
    using NServiceBus.Settings;

    public class AzureServiceBusTransportIntegration : ITransportIntegration
    {
        public AzureServiceBusTransportIntegration()
        {
            ConnectionString = String.Empty; // empty on purpose

            // TODO: This code can be removed when updating to v6 of the transport. Event Subscription Names were no being generated correctly
            // http://stackoverflow.com/questions/24027847/nservicebus-event-subscriptions-not-working-with-azure-service-bus
            AzureServiceBusSubscriptionNamingConvention.Apply = BuildSubscriptionName;
            AzureServiceBusSubscriptionNamingConvention.ApplyFullNameConvention = BuildSubscriptionName;
        }

        static string BuildSubscriptionName(Type eventType)
        {
            var subscriptionName = eventType != null ? Configure.EndpointName + "." + eventType.Name : Configure.EndpointName;

            if (subscriptionName.Length >= 50)
                subscriptionName = new DeterministicGuidBuilder().Build(subscriptionName).ToString();

            if (!SettingsHolder.GetOrDefault<bool>("ScaleOut.UseSingleBrokerQueue"))
                subscriptionName = Individualize(subscriptionName);

            return subscriptionName;
        }

        static string Individualize(string queueName)
        {
            var parser = new ConnectionStringParser();
            var individualQueueName = queueName;
            if (SafeRoleEnvironment.IsAvailable)
            {
                var index = parser.ParseIndexFrom(SafeRoleEnvironment.CurrentRoleInstanceId);

                var currentQueue = parser.ParseQueueNameFrom(queueName);
                if (!currentQueue.EndsWith("-" + index.ToString(CultureInfo.InvariantCulture))) //individualize can be applied multiple times
                {
                    individualQueueName = currentQueue
                                              + (index > 0 ? "-" : "")
                                              + (index > 0 ? index.ToString(CultureInfo.InvariantCulture) : "");
                }
                if (queueName.Contains("@"))
                    individualQueueName += "@" + parser.ParseNamespaceFrom(queueName);
            }

            return individualQueueName;
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

                Parallel.ForEach(subscriptions, subscription =>
                {
                    var topic1 = topic;
                    var subscription1 = subscription;

                    namespaceManager.DeleteSubscription(topic1.Path, subscription1.Name);
                    Console.WriteLine("Deleted subscription '{0}' for topic {1}", subscription1.Name, topic1.Path);
                });

                var topic2 = topic;
                namespaceManager.DeleteTopic(topic2.Path);
                Console.WriteLine("Deleted '{0}' topic", topic2.Path);
            });

            var queues = namespaceManager.GetQueues();
            Parallel.ForEach(queues, queue =>
            {
                var queue1 = queue;
                namespaceManager.DeleteQueue(queue1.Path);
                Console.WriteLine("Deleted '{0}' queue", queue1.Path);
            });
        }
    }
}
