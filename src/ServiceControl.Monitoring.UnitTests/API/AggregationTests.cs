namespace ServiceControl.Monitoring.UnitTests.API
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Web.Http.Results;
    using Http.Diagrams;
    using Messaging;
    using Monitoring.Infrastructure;
    using NUnit.Framework;
    using QueueLength;
    using Timings;

    public class AggregationTests
    {
        ProcessingTimeStore processingTimeStore;
        EndpointRegistry endpointRegistry;
        Settings settings;
        EndpointInstanceActivityTracker activityTracker;
        DiagramApiController apiController;

        [SetUp]
        public void Setup()
        {
            settings = new Settings { EndpointUptimeGracePeriod = TimeSpan.FromMinutes(5) };
            activityTracker = new EndpointInstanceActivityTracker(settings);
            processingTimeStore = new ProcessingTimeStore();
            endpointRegistry = new EndpointRegistry();

            var messageTypeRegistry = new MessageTypeRegistry();
            var breakdownProviders = new IProvideBreakdown[]
            {
                processingTimeStore,
                new CriticalTimeStore(),
                new RetriesStore(),
                new QueueLengthStore()
            };
            apiController = new DiagramApiController(breakdownProviders, endpointRegistry, activityTracker, messageTypeRegistry)
            {
                Request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/monitored-endpoint")
            };
        }

        [Test]
        public void MetricAggregationByInstanceIsScopedToLogicalEndpoint()
        {
            var instanceAId = new EndpointInstanceId("EndpointA", "instance");
            var instanceBId = new EndpointInstanceId("EndpointB", "instance");

            endpointRegistry.Record(instanceAId);
            endpointRegistry.Record(instanceBId);

            var period = HistoryPeriod.FromMinutes(DiagramApiController.DefaultHistory);
            var now = DateTime.UtcNow.Subtract(new TimeSpan(period.IntervalSize.Ticks * period.DelayedIntervals));

            var dataA = new RawMessage.Entry { DateTicks = now.Ticks, Value = 5 };
            var dataB = new RawMessage.Entry { DateTicks = now.Ticks, Value = 10 };

            processingTimeStore.Store(new[] { dataA }, instanceAId, EndpointMessageType.Unknown(instanceAId.EndpointName));
            processingTimeStore.Store(new[] { dataB }, instanceBId, EndpointMessageType.Unknown(instanceBId.EndpointName));

            var result = apiController.GetSingleEndpointMetrics(instanceAId.EndpointName);

            var contentResult = result as OkNegotiatedContentResult<MonitoredEndpointDetails>;
            var model = contentResult.Content;

            Assert.AreEqual(5, model.Instances[0].Metrics["ProcessingTime"].Average);
        }

        [Test]
        public void ValidateIfConnectivityMathIsCorrect()
        {
            var endpointName = "Endpoint";

            var instanceIds = new[] { "a", "b", "c" };
            var instances = instanceIds.Select(instanceId => new EndpointInstanceId(endpointName, instanceId)).ToArray();

            Array.ForEach(instances, instance => endpointRegistry.Record(instance));

            var period = HistoryPeriod.FromMinutes(DiagramApiController.DefaultHistory); // 5 minutes, 5 second period

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

            var result = apiController.GetAllEndpointsMetrics();
            var contentResult = result as OkNegotiatedContentResult<MonitoredEndpoint[]>;
            var model = contentResult.Content;
            var item = model[0];

            Assert.AreEqual(3, item.EndpointInstanceIds.Length, nameof(item.EndpointInstanceIds));
            Assert.AreEqual(2, item.ConnectedCount, nameof(item.ConnectedCount));
            Assert.AreEqual(1, item.DisconnectedCount, nameof(item.DisconnectedCount));
        }
    }
}