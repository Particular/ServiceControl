namespace ServiceControl.Monitoring.PerformanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Dynamic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Http.Diagrams;
    using Messaging;
    using Monitoring.Infrastructure;
    using Nancy;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using QueueLength;
    using Timings;

    public class PerformanceTests : NServiceBusAcceptanceTest
    {
        EndpointRegistry endpointRegistry;
        MessageTypeRegistry messageTypeRegistry;
        CriticalTimeStore criticalTimeStore;
        ProcessingTimeStore processingTimeStore;
        RetriesStore retriesStore;
        QueueLengthStore queueLengthStore;
        DefaultQueueLengthProvider queueLengthProvider;
        Func<Task> GetMonitoredEndpoints;
        Func<string, Task> GetMonitoredSingleEndpoint;
        EndpointInstanceActivityTracker activityTracker;

        [SetUp]
        public void Setup()
        {
            endpointRegistry = new EndpointRegistry();
            criticalTimeStore = new CriticalTimeStore();
            processingTimeStore = new ProcessingTimeStore();
            retriesStore = new RetriesStore();
            queueLengthProvider = new DefaultQueueLengthProvider();
            queueLengthStore = new QueueLengthStore();
            queueLengthProvider.Initialize(string.Empty, queueLengthStore);

            var settings = new Settings { EndpointUptimeGracePeriod = TimeSpan.FromMinutes(5) };
            activityTracker = new EndpointInstanceActivityTracker(settings);

            messageTypeRegistry = new MessageTypeRegistry();

            var breakdownProviders = new IProvideBreakdown[]
            {
                criticalTimeStore,
                processingTimeStore,
                retriesStore,
                queueLengthStore
            };

            var monitoredEndpointsModule = new MonitoredEndpointsModule(breakdownProviders, endpointRegistry, activityTracker, messageTypeRegistry)
            {
                Context = new NancyContext() { Request = new Request("Get", "/monitored-endpoints", "HTTP") }
            };

            var dictionary = monitoredEndpointsModule.Routes.ToDictionary(r => r.Description.Path, r => r.Action);

            GetMonitoredEndpoints = () => dictionary["/monitored-endpoints"](new object(), new CancellationToken(false));
            GetMonitoredSingleEndpoint = endpointName => dictionary["/monitored-endpoints/{endpointName}"](new { EndpointName = endpointName }.ToDynamic(), new CancellationToken());
        }

        [TestCase(10, 10, 100, 1000, 100, 1000)]
        public async Task GetMonitoredEndpointsQueryTest(int numberOfEndpoints, int numberOfInstances, int sendReportEvery, int numberOfEntriesInReport, int queryEveryInMilliseconds, int numberOfQueries)
        {
            var instances = BuildInstances(numberOfEndpoints, numberOfInstances);
            foreach (var instance in instances)
            {
                endpointRegistry.Record(instance);
            }

            var source = new CancellationTokenSource();

            var reporters =
                new[]
                {
                    BuildReporters(sendReportEvery, numberOfEntriesInReport, instances, source, (e, i) => criticalTimeStore.Store(e, i, EndpointMessageType.Unknown(i.EndpointName))),
                    BuildReporters(sendReportEvery, numberOfEntriesInReport, instances, source, (e, i) => processingTimeStore.Store(e, i, EndpointMessageType.Unknown(i.EndpointName))),
                    BuildReporters(sendReportEvery, numberOfEntriesInReport, instances, source, (e, i) => retriesStore.Store(e, i, EndpointMessageType.Unknown(i.EndpointName))),
                    BuildReporters(sendReportEvery, numberOfEntriesInReport, instances, source, (e, i) => queueLengthProvider.Process(i, new TaggedLongValueOccurrence{Entries = e, TagValue = string.Empty}))
                }.SelectMany(i => i).ToArray();

            var histogram = CreateTimeHistogram();

            for (var i = 0; i < numberOfQueries; i++)
            {
                var start = Stopwatch.GetTimestamp();
                await GetMonitoredEndpoints().ConfigureAwait(false);
                var elapsed = Stopwatch.GetTimestamp() - start;
                histogram.RecordValue(elapsed);

                await Task.Delay(queryEveryInMilliseconds);
            }

            source.Cancel();
            await Task.WhenAll(reporters).ConfigureAwait(false);

            var reportFinalHistogram = MergeHistograms(reporters);

            Report("Querying", histogram, TimeSpan.FromMilliseconds(150));
            Report("Reporters", reportFinalHistogram, TimeSpan.FromMilliseconds(20));
        }

        [TestCase(10, 100, 100, 1000, 100, 1000)]
        public async Task GetMonitoredSingleEndpointQueryTest(int numberOfInstances, int numberOfMessageTypes, int sendReportEvery, int numberOfEntriesInReport, int queryEveryInMilliseconds, int numberOfQueries)
        {
            var instances = BuildInstances(1, numberOfInstances);
            foreach (var instance in instances)
            {
                endpointRegistry.Record(instance);
            }

            var endpointName = instances.First().EndpointName;

            for (var i = 0; i < numberOfMessageTypes; i++)
            {
                var messageType = new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0).ToString();
                messageTypeRegistry.Record(new EndpointMessageType(endpointName, messageType));
            }

            var source = new CancellationTokenSource();

            var messageTypes = messageTypeRegistry.GetForEndpointName(endpointName).ToArray();
            long counter = 0;
            Func<EndpointMessageType> getter = () =>
            {
                var value = Interlocked.Increment(ref counter) % messageTypes.Length;
                return messageTypes[value];
            };

            var reporters =
                new[]
                {
                    BuildReporters(sendReportEvery, numberOfEntriesInReport, instances, source, (e, i) => criticalTimeStore.Store(e, i, getter())),
                    BuildReporters(sendReportEvery, numberOfEntriesInReport, instances, source, (e, i) => processingTimeStore.Store(e, i, getter())),
                    BuildReporters(sendReportEvery, numberOfEntriesInReport, instances, source, (e, i) => retriesStore.Store(e, i, getter())),
                    BuildReporters(sendReportEvery, numberOfEntriesInReport, instances, source, (e, i) => queueLengthProvider.Process(i, new TaggedLongValueOccurrence{Entries = e}))
                }.SelectMany(i => i).ToArray();

            var histogram = CreateTimeHistogram();

            for (var i = 0; i < numberOfQueries; i++)
            {
                var start = Stopwatch.GetTimestamp();
                await GetMonitoredSingleEndpoint(endpointName).ConfigureAwait(false);
                var elapsed = Stopwatch.GetTimestamp() - start;
                histogram.RecordValue(elapsed);

                await Task.Delay(queryEveryInMilliseconds).ConfigureAwait(false);
            }

            source.Cancel();
            await Task.WhenAll(reporters).ConfigureAwait(false);

            var reportFinalHistogram = MergeHistograms(reporters);

            Report("Querying", histogram, TimeSpan.FromMilliseconds(25));
            Report("Reporters", reportFinalHistogram, TimeSpan.FromMilliseconds(20));
        }

        static IEnumerable<Task<LongHistogram>> BuildReporters(int sendReportEvery, int numberOfEntriesInReport, EndpointInstanceId[] instances, CancellationTokenSource source, Action<RawMessage.Entry[], EndpointInstanceId> store)
        {
            return instances
                .Select(instance => StartReporter(sendReportEvery, numberOfEntriesInReport, source, instance, store))
                .ToArray();
        }

        static Task<LongHistogram> StartReporter(int sendReportEvery, int numberOfEntriesInReport, CancellationTokenSource source, EndpointInstanceId instance, Action<RawMessage.Entry[], EndpointInstanceId> store)
        {
            return Task.Run(async () =>
            {
                var entries = new RawMessage.Entry[numberOfEntriesInReport];
                var histogram = CreateTimeHistogram();

                while (source.IsCancellationRequested == false)
                {
                    var now = DateTime.UtcNow;

                    for (var i = 0; i < entries.Length; i++)
                    {
                        entries[i].DateTicks = now.AddMilliseconds(100 * i).Ticks;
                        entries[i].Value = i;
                    }

                    var start = Stopwatch.GetTimestamp();
                    store(entries, instance);
                    var elapsed = Stopwatch.GetTimestamp() - start;
                    histogram.RecordValue(elapsed);

                    await Task.Delay(sendReportEvery).ConfigureAwait(false);
                }

                return histogram;
            });
        }

        static EndpointInstanceId[] BuildInstances(int numberOfEndpoints, int numberOfInstances)
        {
            var instances = new List<EndpointInstanceId>();
            for (var i = 0; i < numberOfEndpoints; i++)
            {
                for (var j = 0; j < numberOfInstances; j++)
                {
                    instances.Add(new EndpointInstanceId(i.ToString(), j.ToString()));
                }
            }
            return instances.ToArray();
        }

        static LongHistogram CreateTimeHistogram()
        {
            return new LongHistogram(TimeStamp.Hours(1), 3);
        }

        static LongHistogram MergeHistograms(IEnumerable<Task<LongHistogram>> endpointReporters)
        {
            var result = CreateTimeHistogram();
            foreach (var endpointReporter in endpointReporters)
            {
                result.Add(endpointReporter.Result);
            }
            return result;
        }

        static void Report(string name, LongHistogram histogram, TimeSpan? maximumMean)
        {
            Console.Out.WriteLine($"Histogram for {name}:");
            histogram.OutputPercentileDistribution(Console.Out, outputValueUnitScalingRatio: OutputScalingFactor.TimeStampToMilliseconds, percentileTicksPerHalfDistance: 1);
            Console.Out.WriteLine();

            if (maximumMean != null)
            {
                var max = maximumMean.Value;
                var actualMean = TimeSpan.FromMilliseconds(histogram.GetValueAtPercentile(50) / OutputScalingFactor.TimeStampToMilliseconds);

                Assert.LessOrEqual(actualMean, max, $"The actual mean for {name} was '{actualMean}' and was bigger than maximum allowed mean '{max}'.");
            }
        }
    }

    public static class DynamicExtensions
    {
        public static dynamic ToDynamic<T>(this T obj)
        {
            IDictionary<string, object> expando = new ExpandoObject();

            foreach (var propertyInfo in typeof(T).GetProperties())
            {
                var currentValue = propertyInfo.GetValue(obj);
                expando.Add(propertyInfo.Name, currentValue);
            }
            return (ExpandoObject) expando;
        }
    }
}
