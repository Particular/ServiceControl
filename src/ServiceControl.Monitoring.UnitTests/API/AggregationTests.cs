namespace ServiceControl.Monitoring.UnitTests.API
{
    using System;
    using System.Linq;
    using Messaging;
    using Monitoring.Infrastructure;
    using Monitoring.Infrastructure.Api;
    using NUnit.Framework;
    using QueueLength;
    using Timings;

    [TestFixture]
    public class AggregationTests
    {
        ProcessingTimeStore processingTimeStore;
        EndpointRegistry endpointRegistry;
        Settings settings;
        EndpointInstanceActivityTracker activityTracker;
        IEndpointMetricsApi endpointMetricsApi;

        MessageTypeRegistry messageTypeRegistry;

        [SetUp]
        public void Setup()
        {
            settings = new Settings { EndpointUptimeGracePeriod = TimeSpan.FromMinutes(5) };
            activityTracker = new EndpointInstanceActivityTracker(settings, TimeProvider.System);
            processingTimeStore = new ProcessingTimeStore();
            endpointRegistry = new EndpointRegistry();
            messageTypeRegistry = new MessageTypeRegistry();

            var breakdownProviders = new IProvideBreakdown[]
            {
                processingTimeStore,
                new CriticalTimeStore(),
                new RetriesStore(),
                new QueueLengthStore()
            };
            endpointMetricsApi = new EndpointMetricsApi(breakdownProviders, endpointRegistry, activityTracker,
                messageTypeRegistry);
        }

        [Test]
        public void MetricAggregationByInstanceIsScopedToLogicalEndpoint()
        {
            var instanceAId = new EndpointInstanceId("EndpointA", "instance");
            var instanceBId = new EndpointInstanceId("EndpointB", "instance");

            endpointRegistry.Record(instanceAId);
            endpointRegistry.Record(instanceBId);

            messageTypeRegistry.Record(new EndpointMessageType(instanceAId.EndpointName, enclosedMessageTypes: "MessageA"));
            messageTypeRegistry.Record(new EndpointMessageType(instanceBId.EndpointName, enclosedMessageTypes: "MessageA"));

            var period = HistoryPeriod.FromMinutes(EndpointMetricsApi.DefaultHistory);
            var now = DateTime.UtcNow.Subtract(new TimeSpan(period.IntervalSize.Ticks * period.DelayedIntervals));

            var dataA = new RawMessage.Entry { DateTicks = now.Ticks, Value = 5 };
            var dataB = new RawMessage.Entry { DateTicks = now.Ticks, Value = 10 };

            processingTimeStore.Store([dataA], instanceAId, new EndpointMessageType(instanceAId.EndpointName, enclosedMessageTypes: "MessageA"));
            processingTimeStore.Store([dataB], instanceBId, new EndpointMessageType(instanceBId.EndpointName, enclosedMessageTypes: "MessageA"));

            var logicalEndpointAMetrics = endpointMetricsApi.GetSingleEndpointMetrics(instanceAId.EndpointName);
            var logicalEndpointBMetrics = endpointMetricsApi.GetSingleEndpointMetrics(instanceBId.EndpointName);

            Assert.Multiple(() =>
            {
                Assert.That(logicalEndpointAMetrics.Instances[0].Metrics["ProcessingTime"].Average, Is.EqualTo(5));
                Assert.That(logicalEndpointBMetrics.Instances[0].Metrics["ProcessingTime"].Average, Is.EqualTo(10));

                Assert.That(logicalEndpointAMetrics.MessageTypes[0].Metrics["ProcessingTime"].Average, Is.EqualTo(5));
                Assert.That(logicalEndpointBMetrics.MessageTypes[0].Metrics["ProcessingTime"].Average, Is.EqualTo(10));
            });
        }

        [Test]
        public void ValidateIfConnectivityMathIsCorrect()
        {
            var endpointName = "Endpoint";

            var instanceIds = new[] { "a", "b", "c" };
            var instances = instanceIds.Select(instanceId => new EndpointInstanceId(endpointName, instanceId)).ToArray();

            Array.ForEach(instances, instance => endpointRegistry.Record(instance));

            var period = HistoryPeriod.FromMinutes(EndpointMetricsApi.DefaultHistory); // 5 minutes, 5 second period

            var now = DateTime.UtcNow;
            var timestamp = now.Subtract(new TimeSpan(period.IntervalSize.Ticks * period.DelayedIntervals)); // now - 5 seconds

            var samples = new[]
            {
                new RawMessage.Entry { DateTicks = timestamp.Ticks, Value = 5 },
                new RawMessage.Entry { DateTicks = timestamp.Ticks, Value = 10 }
            };

            var connected = instances.Take(2).ToArray();

            Array.ForEach(connected, instance => activityTracker.Record(instance, now));
            Array.ForEach(connected, instance => processingTimeStore.Store(samples, instance, EndpointMessageType.Unknown(instance.EndpointName)));

            var model = endpointMetricsApi.GetAllEndpointsMetrics();
            var item = model[0];

            Assert.Multiple(() =>
            {
                Assert.That(item.EndpointInstanceIds, Has.Length.EqualTo(3), nameof(item.EndpointInstanceIds));
                Assert.That(item.ConnectedCount, Is.EqualTo(2), nameof(item.ConnectedCount));
                Assert.That(item.DisconnectedCount, Is.EqualTo(1), nameof(item.DisconnectedCount));
            });
        }
    }
}