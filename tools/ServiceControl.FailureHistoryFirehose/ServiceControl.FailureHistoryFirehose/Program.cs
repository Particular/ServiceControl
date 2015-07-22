namespace ServiceControl.FailureHistoryFirehose
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.AspNet.SignalR.Client;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;

    class Program
    {
        private const int MESSAGE_COUNT = 1000;
        private const string SERVICE_CONTROL_SIGNALR = "http://localhost:33333/api/messagestream";

        static void Main(string[] args)
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

                var signalR = new Connection(SERVICE_CONTROL_SIGNALR);
                var finished = new AutoResetEvent(false);

                var count = 0;
                signalR.Received += s =>
                {
                    if (s.Contains(@"""MessageFailures"""))
                    {
                        count++;
                        Console.WriteLine(count);
                        if (count == MESSAGE_COUNT)
                            finished.Set();
                    }
                };

                signalR.Start().Wait();
                Console.WriteLine("SignalR Connection established");

                var stopWatch = new Stopwatch();
                stopWatch.Start();
                for (var i = 0; i < MESSAGE_COUNT; i++)
                    bus.SendLocal<PerformSomeTaskThatFails>(m => m.Id = i);

                finished.WaitOne();
                stopWatch.Stop();
                Console.WriteLine("{0} messages sent. Took {1}", MESSAGE_COUNT, stopWatch.Elapsed);
                Console.WriteLine("Press ENTER to quit");
                Console.ReadLine();

                // Causes a failure in ServiceControl
                // signalR.Stop();
            }
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
