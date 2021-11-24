namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Messaging;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Transport;
    using ServiceControlInstaller.Engine.Instances;
    using Transports.Msmq;

    public class ConfigureEndpointMsmqTransport : ITransportIntegration
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            var settingsHolder = configuration.GetSettings();
            queueBindings = settingsHolder.Get<QueueBindings>();

            var transportConfig = configuration.UseTransport<MsmqTransport>();

            transportConfig.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
            transportConfig.DisableConnectionCachingForSends();
            settingsHolder.Set("NServiceBus.Transport.Msmq.MessageEnumeratorTimeout", TimeSpan.FromMilliseconds(10));

            var routingConfig = transportConfig.Routing();

            foreach (var publisher in publisherMetadata.Publishers)
            {
                foreach (var eventType in publisher.Events)
                {
                    routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
                }
            }

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            var allQueues = MessageQueue.GetPrivateQueuesByMachine("localhost");
            var queuesToBeDeleted = new List<string>();

            foreach (var messageQueue in allQueues)
            {
                using (messageQueue)
                {
                    if (queueBindings.ReceivingAddresses.Any(ra =>
                    {
                        var indexOfAt = ra.IndexOf("@", StringComparison.Ordinal);
                        if (indexOfAt >= 0)
                        {
                            ra = ra.Substring(0, indexOfAt);
                        }

                        return messageQueue.QueueName.StartsWith(@"private$\" + ra, StringComparison.OrdinalIgnoreCase);
                    }))
                    {
                        queuesToBeDeleted.Add(messageQueue.Path);
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
                catch (Exception)
                {
                    Console.WriteLine("Could not delete queue '{0}'", queuePath);
                }
            }

            MessageQueue.ClearConnectionCache();

            return Task.FromResult(0);
        }

        public string ScrubPlatformConnection(string input)
        {
            var result = input.Replace(
                Environment.MachineName,
                "MACHINE_NAME"
            );

            return result;
        }

        public string Name => TransportNames.MSMQ;
        public string TypeName => $"{typeof(MsmqTransportCustomization).AssemblyQualifiedName}";
        public string ConnectionString { get; set; }

        QueueBindings queueBindings;
    }
}