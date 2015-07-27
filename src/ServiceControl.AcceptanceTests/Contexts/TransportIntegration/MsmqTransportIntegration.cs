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
        public Type Type { get { return typeof(Msmq); } }
        public string TypeName { get { return "NServiceBus.Msmq, NServiceBus.Core"; }}
        public string ConnectionString { get; set; }

        public void Cleanup(ITransportIntegration transport)
        {
            var name = Configure.EndpointName;
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
                    }
                }
            }

            foreach (var queuePath in queuesToBeDeleted)
            {
                MessageQueue.Delete(queuePath);
                Console.WriteLine("Deleted '{0}' queue", queuePath);
            }

            MessageQueue.ClearConnectionCache();
        }
    }
}
