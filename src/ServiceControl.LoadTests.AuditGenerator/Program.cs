namespace ServiceControl.LoadTests.AuditGenerator
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Features;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using ServiceControl.Infrastructure.Metrics;
    using Transports;

    class Program
    {
        public static readonly string HostId = Guid.NewGuid().ToString();

        public static IDispatchMessages Dispatcher { get; set; }

        static async Task Main(string[] commandLineArgs)
        {
            bodySize = int.Parse(commandLineArgs[0]);
            var transportCustomizationName = commandLineArgs.Length > 1
                ? commandLineArgs[1]
                : QueueLengthProviderMsmqTransportCustomizationName;

            var connectionString = commandLineArgs.Length > 2 ? commandLineArgs[2] : null;

            var transportCustomization = (TransportCustomization)Activator.CreateInstance(Type.GetType(transportCustomizationName, true));
            var queueLengthProvider = transportCustomization.CreateQueueLengthProvider();
            queueLengthProvider.Initialize(connectionString, CacheQueueLength);

            var configuration = new EndpointConfiguration("AuditGenerator");
            configuration.EnableFeature<CaptureDispatcherFeature>();
            configuration.SendOnly();
            configuration.UseTransport<MsmqTransport>();
            configuration.UsePersistence<InMemoryPersistence>();
            configuration.EnableInstallers();

            GenerateLayouts();

            var metrics = new Metrics();
            reporter = new MetricsReporter(metrics, Console.WriteLine, TimeSpan.FromSeconds(2));
            counter = metrics.GetCounter("Messages sent");

            var endpoint = await Endpoint.Start(configuration);

            var commands = new (string, Func<CancellationToken, string[], Task>)[]
            {
                ("t|Throttled sending that keeps the receiver queue size at n. Syntax: t <number of msgs in queue> <destination>",
                    (ct, args) => ConstantQueueLengthSend(args, Dispatcher, queueLengthProvider, ct)),
                ("c|Constant-throughput sending. Syntax: c <number of msgs per second> <destination>",
                    (ct, args) => ConstantThroughputSend(args, Dispatcher, ct))
            };

            await queueLengthProvider.Start();

            await Run(commands);

            await queueLengthProvider.Stop();
        }

        class CaptureDispatcherFeature : Feature
        {
            protected override void Setup(FeatureConfigurationContext context)
            {
                context.Container.ConfigureComponent<CaptureDispatcherStartupTask>(DependencyLifecycle.SingleInstance);
                context.RegisterStartupTask(b => b.Build<CaptureDispatcherStartupTask>());
            }

            class CaptureDispatcherStartupTask : FeatureStartupTask
            {
                public CaptureDispatcherStartupTask(IDispatchMessages dispatchMessages)
                {
                    Dispatcher = dispatchMessages;
                }

                protected override Task OnStart(IMessageSession session)
                {
                    return Task.CompletedTask;
                }

                protected override Task OnStop(IMessageSession session)
                {
                    return Task.CompletedTask;
                }
            }
        }

        static void GenerateLayouts()
        {
            var random = new Random();
            for (var i = 0; i < numberOfLayouts; i++)
            {
                var messageTypeName = RandomString(MessageTypeNameAndValueLength, random).FirstCharToUpper();
                var messageTypeNameLength = Encoding.UTF8.GetBytes(messageTypeName).Length;
                var layoutBuilder = new StringBuilder();
                layoutBuilder.AppendLine();
                var propertyPositionPlaceHolder = 0;
                for (var j = 1; j < bodySize; j++)
                {
                    var propertyName = RandomString(random.Next(10, 30), random).FirstCharToUpper();
                    layoutBuilder.AppendLine(
                        $"   <{propertyName}>{{{propertyPositionPlaceHolder}}}</{propertyName}>");
                    propertyPositionPlaceHolder++;

                    var intermediateLayout = MessageBaseLayout.Replace("__MESSAGETYPENAME__", messageTypeName)
                        .Replace("__CONTENT__", layoutBuilder.ToString());
                    // saving 20 chars per property
                    if ((Encoding.UTF8.GetBytes(intermediateLayout).Length + (i * messageTypeNameLength)) >= bodySize)
                    {
                        MessageBaseLayouts.Add((intermediateLayout, propertyPositionPlaceHolder));
                        break;
                    }
                }
            }
        }

        public static string RandomString(int length, Random random)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Range(1, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }

        static void CacheQueueLength(QueueLengthEntry[] values, EndpointToQueueMapping queueAndEndpointName)
        {
            var newValue = (int)values.OrderBy(x => x.DateTicks).Last().Value;
            QueueLengths.AddOrUpdate(queueAndEndpointName.InputQueue, newValue, (queue, oldValue) => newValue);
        }

        static Task SendAuditMessage(IDispatchMessages dispatcher, string destination, Random random)
        {
            // because MSMQ is essentially synchronous
            return Task.Run(async () =>
            {
                var now = DateTime.UtcNow;

                var headers = new Dictionary<string, string>
                {
                    [Headers.ContentType] = "application/xml",
                    [Headers.HostId] = HostId,
                    [Headers.HostDisplayName] = "Load Generator",
                    [Headers.ProcessingMachine] = RuntimeEnvironment.MachineName,
                    [Headers.ProcessingEndpoint] = $"LoadGenerator{random.Next(1, numberOfEndpoints)}",
                    [Headers.ProcessingStarted] = DateTimeExtensions.ToWireFormattedString(now),
                    [Headers.ProcessingEnded] = DateTimeExtensions.ToWireFormattedString(now.AddMilliseconds(random.Next(10, 100))),
                };

                var layoutIndex = random.Next(0, numberOfLayouts);
                var (layout, numberOfProperties) = MessageBaseLayouts[layoutIndex];
                var propertyValues = new List<object>(numberOfProperties);
                for (var i = 0; i < numberOfProperties; i++)
                {
                    propertyValues.Add(RandomString(MessageTypeNameAndValueLength, random));
                }

                var transportOperation = new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), headers,
                        Encoding.UTF8.GetBytes(string.Format(layout, propertyValues.ToArray()))),
                    new UnicastAddressTag(destination), DispatchConsistency.Isolated);

                await dispatcher.Dispatch(new TransportOperations(transportOperation), new TransportTransaction(), new ContextBag());
                counter.Mark();
            });
        }

        static async Task ConstantQueueLengthSend(string[] args, IDispatchMessages dispatcher, IProvideQueueLength queueLengthProvider, CancellationToken ct)
        {
            var maxSenderCount = 20;
            var taskBarriers = new int[maxSenderCount];

            var numberOfMessages = int.Parse(args[0]);
            var destination = args.Length > 1 ? args[1] : DefaultDestination;

            queueLengthProvider.TrackEndpointInputQueue(new EndpointToQueueMapping(destination, destination));

            var monitor = Task.Run(async () =>
            {
                var nextTask = 0;

                while (ct.IsCancellationRequested == false)
                {
                    try
                    {
                        if (!QueueLengths.TryGetValue(destination, out var queueLength))
                        {
                            queueLength = 0;
                        }

                        Console.WriteLine($"Current queue length: {queueLength}");

                        var delta = numberOfMessages - queueLength;

                        if (delta > 0)
                        {
                            Interlocked.Exchange(ref taskBarriers[nextTask], 1);

                            nextTask = Math.Min(taskBarriers.Length, nextTask + 1);
                        }
                        else
                        {
                            nextTask = Math.Max(0, nextTask - 1);

                            Interlocked.Exchange(ref taskBarriers[nextTask], 0);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(2), ct);
                    }
                    catch
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
            }, ct);

            var senders = Enumerable.Range(0, taskBarriers.Length - 1).Select(async taskNo =>
            {
                var random = new Random(Environment.TickCount + taskNo);
                while (ct.IsCancellationRequested == false)
                {
                    try
                    {
                        var allowed = Interlocked.CompareExchange(ref taskBarriers[taskNo], 1, 1);

                        if (allowed == 1)
                        {
                            await SendAuditMessage(dispatcher, destination, random).ConfigureAwait(false);
                        }
                        else
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1), ct);
                        }
                    }
                    catch
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
            }).ToArray();

            await Task.WhenAll(new List<Task>(senders) { monitor });
        }

        static async Task ConstantThroughputSend(string[] args, IDispatchMessages dispatcher, CancellationToken ct)
        {
            var messagesPerSecond = int.Parse(args[0]);
            var destination = args.Length > 1 ? args[1] : DefaultDestination;

            var senders = Enumerable.Range(0, messagesPerSecond).Select(async taskNo =>
            {
                var random = new Random(Environment.TickCount + taskNo);
                while (ct.IsCancellationRequested == false)
                {
                    try
                    {
                        var sendAuditMessage = SendAuditMessage(dispatcher, destination, random);
                        var delay = Task.Delay(1000, ct);
                        var resultTask = await Task.WhenAny(sendAuditMessage, delay);
                        if (resultTask == sendAuditMessage)
                        {
                            await delay;
                        }
                    }
                    catch
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }
                        throw;
                    }
                }
            }).ToArray();

            await Task.WhenAll(senders);
        }

        static async Task Run((string, Func<CancellationToken, string[], Task>)[] commands)
        {
            Console.WriteLine("Select command:");
            commands.Select(i => i.Item1).ToList().ForEach(Console.WriteLine);

            while (true)
            {
                var commandLine = Console.ReadLine();
                if (commandLine == null)
                {
                    continue;
                }

                var parts = commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var key = parts.First().ToLowerInvariant();
                var arguments = parts.Skip(1).ToArray();

                var match = commands.Where(c => c.Item1.StartsWith(key)).ToArray();

                if (match.Any())
                {
                    var command = match.First();

                    Console.WriteLine($"\nExecuting: {command.Item1.Split('|')[1]}");

                    reporter.Start();

                    using (var ctSource = new CancellationTokenSource())
                    {
                        var task = command.Item2(ctSource.Token, arguments);

                        while (ctSource.IsCancellationRequested == false && task.IsCompleted == false)
                        {
                            if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Enter)
                            {
                                ctSource.Cancel();
                                break;
                            }

                            await Task.Delay(TimeSpan.FromMilliseconds(500), ctSource.Token);
                        }

                        await task;
                    }

                    await reporter.Stop();

                    Console.WriteLine("Done");
                }
            }
        }

        static readonly string QueueLengthProviderMsmqTransportCustomizationName = typeof(MsmqTransportCustomizationWithQueueLengthProvider).AssemblyQualifiedName;
        const string DefaultDestination = "audit";
        const int MessageTypeNameAndValueLength = 20;
        static readonly ConcurrentDictionary<string, int> QueueLengths = new ConcurrentDictionary<string, int>();
        static int bodySize;
        static Counter counter;
        static MetricsReporter reporter;
        static int numberOfLayouts = 200;
        static int numberOfEndpoints = 40;

        static readonly string MessageBaseLayout =
            @"<__MESSAGETYPENAME__ xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""http://tempuri.net/NServiceBus.Serializers.XML.Test"">__CONTENT__</__MESSAGETYPENAME__>";

        static readonly List<(string layout, int numberOfProperties)> MessageBaseLayouts = new List<(string layout, int numberOfProperties)>();
    }
}