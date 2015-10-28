using System;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Logging;

class Program
{
    private const int MESSAGE_COUNT = 1000;

    private static void Main()
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
}