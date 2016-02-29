namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Messaging;
    using NServiceBus;

    public class MsmqTransportIntegration : ITransportIntegration
    {
        public MsmqTransportIntegration()
        {
            ConnectionString = @"cacheSendConnection=false;journal=false;"; // Default connstr
        }

        public string Name { get { return "Msmq"; } }
        public Type Type { get { return typeof(MsmqTransport); } }
        public string TypeName { get { return "NServiceBus.MsmqTransport, NServiceBus.Core"; }}
        public string ConnectionString { get; set; }

        public void OnEndpointShutdown(string endpointName)
        {
            DeleteQueues(endpointName);
        }

        public void TearDown()
        {
            DeleteQueues("error");
            DeleteQueues("audit");
        }

        static void DeleteQueues(string name)
        {
            var nameFilter = @"private$\" + name;
            var allQueues = MessageQueue.GetPrivateQueuesByMachine("localhost");
            var queuesToBeDeleted = new List<string>();

            foreach (var messageQueue in allQueues)
            {
                using (messageQueue)
                {
                    if (messageQueue.QueueName.StartsWith(nameFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        queuesToBeDeleted.Add(messageQueue.Path);
                        Console.WriteLine("Deleted '{0}' queue", messageQueue.Path);
                    }
                }
            }

            foreach (var queuePath in queuesToBeDeleted)
            {
                try
                {
                    MessageQueue.Delete(queuePath);
                    Console.WriteLine("Deleted '{0}' queue", queuePath);
                }
                catch (MessageQueueException)
                {
                    //There could be a concurrency problem deleting error and audit queues
                }
            }

            MessageQueue.ClearConnectionCache();
        }
    }
}
