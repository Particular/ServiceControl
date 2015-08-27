using System;
using System.Threading;
using NServiceBus;
using NServiceBus.Config;
using NServiceBus.Config.ConfigurationSource;
using NServiceBus.Features;
using NServiceBus.Logging;

namespace ErrorsGenerator
{
    class Program
    {
        private static int ERROR_MESSAGE_GENERATED_EVERY_MILLISECONDS = 2000;
        private const int MESSAGE_COUNT = 10000;

        static void Main()
        {
            LogManager.Use<DefaultFactory>().Level(LogLevel.Error);

            var config = new BusConfiguration();
            config.EndpointName("ErrorsGenerator");
            config.UseSerialization<JsonSerializer>();
            config.UsePersistence<InMemoryPersistence>();
            config.DisableFeature<SecondLevelRetries>();
            config.EnableInstallers();

            using (var bus = Bus.Create(config))
            {
                bus.Start();
                Console.WriteLine("Bus Started");

                for (var i = 0; i < MESSAGE_COUNT; i++)
                {
                    bus.SendLocal<PerformSomeTaskThatFails>(m => m.Id = i);
                    Thread.Sleep(ERROR_MESSAGE_GENERATED_EVERY_MILLISECONDS);
                }

                Console.WriteLine("Press ENTER to quit");
                Console.ReadLine();
            }
        }
    }

    public class PerformSomeTaskThatFails : ICommand
    {
        public int Id { get; set; }
    }

    public class FailingHandler : IHandleMessages<PerformSomeTaskThatFails>
    {
        public IBus Bus { get; set; }
        public void Handle(PerformSomeTaskThatFails message)
        {
            throw new Exception("This operation has failed");
        }
    }

    class TurnOffFirstLevelRetries : IProvideConfiguration<TransportConfig>
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