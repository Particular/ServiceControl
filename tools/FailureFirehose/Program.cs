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
using ServiceControl.Plugin.CustomChecks;
using ServiceControl.Pump;

namespace FailureFirehose
{
    internal class Program
    {
        public const int MAX_BODY_SIZE = 1024 * 1024; // 2MB
        public const int MIM_BODY_SIZE = 70;
        public const int MAX_MESSAGE_COUNT = 350;
        public const int MIN_MESSAGE_COUNT = 50;
        public const int FAILURE_PERCENTAGE = 5;

        private static void Main()
        {
            LogManager.Use<DefaultFactory>()
                .Level(LogLevel.Fatal);

            var source = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, args) =>
            {
                args.Cancel = true;
                source.Cancel();
            };

            Console.WriteLine("Mode of operation:");
            Console.WriteLine("1 - Sender");
            Console.WriteLine("2 - Receiver");
            Console.WriteLine("3 - Retry/Archive");
            Console.WriteLine("4 - Receiver with pump");

            Action action = () => {};
            bool wrongOption;

            do
            {
                var key = Console.ReadKey(true);
                wrongOption = false;

                switch (key.KeyChar)
                {
                    case '1':
                        action = () => RunSender(source.Token);
                        break;
                    case '2':
                        action = () => RunReceiver(source.Token);
                        break;

                    case '4':
                        action = () => RunReceiver(source.Token, true);
                        break;

                    case '3':
                        action = () => ExecuteRestAPI(source.Token);
                        break;

                    default:
                        Console.Out.WriteLine("Invalid option, try again!");
                        wrongOption = true;
                        break;

                }
            } while (wrongOption);

            Console.WriteLine("Press Ctrl+C to exit");

            Task.Run(action, CancellationToken.None).GetAwaiter().GetResult();
               
        }

        private static void ExecuteRestAPI(CancellationToken token)
        {
            var timer = new Timer(state =>
            {
                Console.WriteLine("Firing off Archive/Retry Requests");
                ServiceControl.ArchiveAllGroups();
            }, null, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(20));

            token.WaitHandle.WaitOne();
            using (var manualResetEvent = new ManualResetEvent(false))
            {
                timer.Dispose(manualResetEvent);
            }
        }

        static void RunReceiver(CancellationToken token, bool pumpOn = false)
        {
            var config = new EndpointConfiguration("FailureFirehose_Receiver");
            config.UseTransport<MsmqTransport>().Transactions(TransportTransactionMode.None);
            config.UsePersistence<InMemoryPersistence>();
            config.Recoverability().Delayed(settings => settings.NumberOfRetries(0));
            config.AuditProcessedMessagesTo("FailureFirehose_Receiver.ServiceControl");
            if (pumpOn)
            {
                config.EnableFeature<ServiceControlPump>();
            }
            config.EnableInstallers();
            config.LimitMessageProcessingConcurrencyTo(30);
            config.Recoverability().Immediate(settings => settings.NumberOfRetries(5));
            config.CustomCheckPlugin("particular.servicecontrol");
            ServicePointManager.DefaultConnectionLimit = 300;
            var endpoint = Endpoint.Start(config).GetAwaiter().GetResult();
            Console.WriteLine("Receiver Bus Started");
            token.WaitHandle.WaitOne();
            endpoint.Stop().GetAwaiter().GetResult();
        }

        private static void RunSender(CancellationToken token)
        {
            var config = new EndpointConfiguration("FailureFirehose");
            config.UseTransport<MsmqTransport>().Transactions(TransportTransactionMode.None);
            config.UsePersistence<InMemoryPersistence>();
            config.Recoverability().Delayed(settings => settings.NumberOfRetries(0));
            config.EnableInstallers();
            config.LimitMessageProcessingConcurrencyTo(30);
            config.Recoverability().Immediate(settings => settings.NumberOfRetries(5));
            config.CustomCheckPlugin("particular.servicecontrol");

            var rnd = new Random();

            var body = new string('a', MAX_BODY_SIZE);

            var bus = Endpoint.Start(config).GetAwaiter().GetResult();

            Console.WriteLine("Sender Bus Started");

            var iteration = 1;

            while (!token.IsCancellationRequested)
            {
                var total = rnd.Next(MIN_MESSAGE_COUNT, MAX_MESSAGE_COUNT + 1);
                Console.Write($"Iteration {iteration++}, sending {total} message(s)");
                var stopwatch = new Stopwatch();

                stopwatch.Start();
                var failureThreashold = (int) (total*(FAILURE_PERCENTAGE/100m));

                Parallel.For(0, total, i =>
                {
                    PerformSomeTaskThatFails performSomeTaskThatFails;
                    unsafe
                    {
                        fixed (char* p = body)
                        {
                            performSomeTaskThatFails = new PerformSomeTaskThatFails
                            {
                                Id = i,
                                FailureThreashold = failureThreashold,
                                Body = new string(p, 0, rnd.Next(MIM_BODY_SIZE, MAX_BODY_SIZE))
                                // Saving on memory allocations by using a pointer
                            };
                        }
                    }
                    bus.Send("FailureFirehose_Receiver", performSomeTaskThatFails).GetAwaiter().GetResult();
                });

                stopwatch.Stop();

                Console.Write($", completed in {stopwatch.Elapsed}");
                Console.WriteLine();

                var remainingTime = (int) (TimeSpan.FromSeconds(1) - stopwatch.Elapsed).TotalMilliseconds;
                if (remainingTime > 0)
                {
                    Thread.Sleep(remainingTime);
                }

                if (iteration > 20*60) // About every 20 minutes
                {
                    Console.WriteLine("Restarting Iterations");
                    iteration = 1;
                }
            }

            bus.Stop().GetAwaiter().GetResult();
        }
    }

    public class MyCheck : CustomCheck
    {
        Random rnd = new Random();

        public MyCheck() : base("MyCheck", "Testing", TimeSpan.FromMinutes(1))
        {
        }

        public override Task<CheckResult> PerformCheck()
        {
            if (rnd.Next(0, 1) == 0)
            {
                return CheckResult.Pass;
            }

            return CheckResult.Failed("Because I can");
        }
    }

    public class MyCheck2 : CustomCheck
    {
        Random rnd = new Random();

        public MyCheck2() : base("MyCheck2", "Testing", TimeSpan.FromSeconds(45))
        {
        }

        public override Task<CheckResult> PerformCheck()
        {
            if (rnd.Next(0, 1) == 0)
            {
                return CheckResult.Pass;
            }

            return CheckResult.Failed("Because I can");
        }
    }

    public class PerformSomeTaskThatFails : ICommand
    {
        public int Id { get; set; }
        public string Body { get; set; }
        public int FailureThreashold { get; set; }
    }

    public class FailingHandler : IHandleMessages<PerformSomeTaskThatFails>
    {
        public Task Handle(PerformSomeTaskThatFails message, IMessageHandlerContext context)
        {
            if (message.Id < message.FailureThreashold)
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
