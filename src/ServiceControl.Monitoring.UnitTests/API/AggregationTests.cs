namespace ServiceControl.Monitoring.UnitTests.API
{
    using System;
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
        [Test]
        public void MetricAggregationByInstanceIsScopedToLogicalEndpoint()
        {
            var processingTimeStore = new ProcessingTimeStore();
            var endpointRegistry = new EndpointRegistry();

            var apiController = CreateConroller(processingTimeStore, endpointRegistry);

            var instanceAId = new EndpointInstanceId("EndpointA", "instance");
            var instanceBId = new EndpointInstanceId("EndpointB", "instance");

            endpointRegistry.Record(instanceAId);
            endpointRegistry.Record(instanceBId);

            var period = HistoryPeriod.FromMinutes(DiagramApiController.DefaultHistory);
            var now = DateTime.UtcNow.Subtract(new TimeSpan(period.IntervalSize.Ticks * period.DelayedIntervals));

            var dataA = new RawMessage.Entry {DateTicks = now.Ticks, Value = 5};
            var dataB = new RawMessage.Entry {DateTicks = now.Ticks, Value = 10};

            processingTimeStore.Store(new[] {dataA}, instanceAId, EndpointMessageType.Unknown(instanceAId.EndpointName));
            processingTimeStore.Store(new[] {dataB}, instanceBId, EndpointMessageType.Unknown(instanceBId.EndpointName));

            var result = apiController.GetSingleEndpointMetrics(instanceAId.EndpointName);

            var contentResult = result as OkNegotiatedContentResult<MonitoredEndpointDetails>;
            var model = contentResult.Content;

            Assert.AreEqual(5, model.Instances[0].Metrics["ProcessingTime"].Average);
        }

        static DiagramApiController CreateConroller(ProcessingTimeStore processingTimeStore, EndpointRegistry endpointRegistry)
        {
            var criticalTimeStore = new CriticalTimeStore();
            var retriesStore = new RetriesStore();
            var queueLengthStore = new QueueLengthStore();

            var settings = new Settings
            {
                EndpointUptimeGracePeriod = TimeSpan.FromMinutes(5)
            };
            var activityTracker = new EndpointInstanceActivityTracker(settings);

            var messageTypeRegistry = new MessageTypeRegistry();

            var breakdownProviders = new IProvideBreakdown[]
            {
                processingTimeStore,
                criticalTimeStore,
                retriesStore,
                queueLengthStore
            };

            var controller = new DiagramApiController(breakdownProviders, endpointRegistry, activityTracker, messageTypeRegistry)
            {
                Request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/monitored-endpoint")
            };

            return controller;
        }
    }
}