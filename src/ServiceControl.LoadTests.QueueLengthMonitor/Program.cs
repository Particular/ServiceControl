using System;

namespace ServiceControl.LoadTests.QueueLengthMonitor
{
    using System.Messaging;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Support;
    using ServiceControl.LoadTests.Messages;

    class Program
    {
        static void Main(string[] args)
        {
            Start(args).GetAwaiter().GetResult();
        }

        static async Task Start(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: QueueLengthMonitor <monitored-queue> <report-destination-queue@machine>");
                return;
            }

            var queueName = args[0];
            var destination = args[1];

            MessageQueue.ClearConnectionCache();

            var config = new EndpointConfiguration("QueueLengthMonitor");
            config.UseTransport<MsmqTransport>();
            config.SendOnly();
            config.UsePersistence<InMemoryPersistence>();

            var endpoint = await Endpoint.Start(config);

            while (true)
            {
                var messageQueue = new MessageQueue($@".\private$\{queueName}", QueueAccessMode.Peek);

                var count = messageQueue.GetCount();

                var message = new QueueLengthReport
                {
                    Length = count,
                    Machine = RuntimeEnvironment.MachineName,
                    Queue = queueName
                };

                await endpoint.Send(destination, message).ConfigureAwait(false);

                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
        }
    }
}