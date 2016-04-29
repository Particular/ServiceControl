using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Logging;

namespace FailureFirehose
{
    internal class Program
    {
        public const int MAX_BODY_SIZE = 10000;
        public const int MESSAGE_COUNT = 350;
        public const int FAILURE_PERCENTAGE = 5;
        public const int FAILURE_THRESHOLD = (int)(MESSAGE_COUNT * (FAILURE_PERCENTAGE / 100m));

        private static void Main()
        {
            LogManager.Use<DefaultFactory>()
                .Level(LogLevel.Fatal);

            var config = new EndpointConfiguration("FailureFirehose");
            config.UseTransport<MsmqTransport>().Transactions(TransportTransactionMode.None);
            config.UseSerialization<NServiceBus.JsonSerializer>();
            config.UsePersistence<InMemoryPersistence>();
            config.DisableFeature<SecondLevelRetries>();
            config.EnableFeature<Audit>();
            config.EnableInstallers();

            var alphabet = " abcdefghijklmnopqrstuzwxyz".ToArray();

            var rnd = new Random();

            var bodies = new string[MESSAGE_COUNT];

            for (var i = 0; i < MESSAGE_COUNT; i++)
            {
                bodies[i] = new string(Enumerable.Range(0, rnd.Next(MAX_BODY_SIZE))
                    .Select(x => alphabet.OrderBy(y => rnd.Next()).First())
                    .ToArray());
            }


            var bus = Endpoint.Start(config).GetAwaiter().GetResult();

            Console.WriteLine("Bus Started");

            var iteration = 1;

            while (true)
            {
                Console.WriteLine($"Iteration {iteration++}");
                var stopwatch = new Stopwatch();

                stopwatch.Start();
                Parallel.ForEach(
                    Enumerable.Range(0, MESSAGE_COUNT),
                    i => bus.SendLocal(new PerformSomeTaskThatFails
                    {
                        Id = i,
                        Body = bodies[i]
                    }));

                if ((iteration%200) == 0)
                {
                    Thread.Sleep(TimeSpan.FromMinutes(2));
                }

                if (iteration > 60*60 /* one hour or so */)
                {
                    Console.WriteLine("Firing off Archive Requests");
                    ServiceControl.ArchiveAllGroups();
                    Console.WriteLine("Restarting Iterations");
                    iteration = 1;
                }

                stopwatch.Stop();
                var remainingTime = (int) (TimeSpan.FromSeconds(1) - stopwatch.Elapsed).TotalMilliseconds;
                if (remainingTime > 0)
                    Thread.Sleep(remainingTime);
                else
                {
                    Console.WriteLine("Running Late!");
                }
            }

        }

    }
    public class PerformSomeTaskThatFails : ICommand
    {
        public int Id { get; set; }
        public string Body { get; set; }
    }


    public class FailingHandler : IHandleMessages<PerformSomeTaskThatFails>
    {
        public Task Handle(PerformSomeTaskThatFails message, IMessageHandlerContext context)
        {
            if (message.Id < Program.FAILURE_THRESHOLD)
            {
                switch (message.Id % 4)
                {
                    case 0: throw new IOException("The disk is full");
                    case 1: throw new WebException("The web API isn't responding");
                    case 2: throw new SerializationException("Cannot deserialize message");
                    default: throw new Exception("Some business thing happened");
                }
            }

            return Task.FromResult(0);
        }
    }

    public static class ServiceControl
    {
        static readonly Random rand = new Random();

        public static void ArchiveAllGroups()
        {
            using (var client = new WebClient())
            {
                var response = client.DownloadString("http://localhost:33333/api/recoverability/groups");

                var groups = JsonConvert.DeserializeObject<Group[]>(response);
                var retries = rand.Next(0, groups.Length);
                var ids = groups.Select(g => g.Id).ToArray();

                for (var i = 0; i < retries; i++)
                {
                    client.UploadData($"http://localhost:33333/api/recoverability/groups/{ids[i]}/errors/retry", "POST", new byte[0]);
                }

                for (var i = retries; i < groups.Length; i++)
                {
                    client.UploadData($"http://localhost:33333/api/recoverability/groups/{ids[i]}/errors/archive", "POST", new byte[0]);
                }

            }
        }
    }

    class Group
    {
        public string Id { get; set; }
    }
}
