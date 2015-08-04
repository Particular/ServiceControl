using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using NServiceBus;
using NServiceBus.Config;
using NServiceBus.Config.ConfigurationSource;
using NServiceBus.Features;
using NServiceBus.Logging;

namespace FailureFirehose
{
    internal class Program
    {
        private const int MESSAGE_COUNT = 1000;

        private static void Main(string[] args)
        {
            LogManager.Use<DefaultFactory>()
                .Level(LogLevel.Error);

            var config = new BusConfiguration();
            config.EndpointName("Failure History Firehose");
            config.UseSerialization<JsonSerializer>();
            config.UsePersistence<InMemoryPersistence>();
            config.DisableFeature<SecondLevelRetries>();
            config.EnableInstallers();

            using (var bus = Bus.Create(config))
            {
                bus.Start();
                Console.WriteLine("Bus Started");

                for (var i = 0; i < MESSAGE_COUNT; i++)
                    bus.SendLocal<PerformSomeTaskThatFails>(m => m.Id = i);

                Console.WriteLine("{0} messages sent.", MESSAGE_COUNT);
                Console.WriteLine("Press ENTER to quit");
                Console.ReadLine();
            }
        }

        public class PerformSomeTaskThatFails : ICommand
        {
            public int Id { get; set; }
        }

        public class FailingHandler : IHandleMessages<PerformSomeTaskThatFails>
        {
            public void Handle(PerformSomeTaskThatFails message)
            {
                switch (message.Id % 4)
                {
                    case 0: throw new IOException("The disk is full");
                    case 1: throw new WebException("The web API isn't responding");
                    case 2: throw new SerializationException("Cannot deserialize message");
                    default: throw new Exception("Some business thing happened");
                }
                Console.WriteLine("Message processed successfully");
            }
        }

        public class TurnOffFirstLevelRetries : IProvideConfiguration<TransportConfig>
        {
            public TransportConfig GetConfiguration()
            {
                return new TransportConfig
                {
                    MaxRetries = 1
                };
            }
        }
    }
}




