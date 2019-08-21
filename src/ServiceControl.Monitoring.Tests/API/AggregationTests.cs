namespace ServiceControl.Monitoring.Tests.API
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Http.Diagrams;
    using Messaging;
    using Monitoring.Infrastructure;
    using QueueLength;
    using Nancy;
    using NUnit.Framework;
    using Timings;

    public class AggregationTests
    {
        [Test]
        public async Task MetricAggregationByInstanceIsScopedToLogicalEndpoint()
        {
            var processingTimeStore = new ProcessingTimeStore();
            var endpointRegistry = new EndpointRegistry();

            var queryAction = CreateQuery(processingTimeStore, endpointRegistry);

            var instanceAId = new EndpointInstanceId("EndpointA", "instance");
            var instanceBId = new EndpointInstanceId("EndpointB", "instance");

            endpointRegistry.Record(instanceAId);
            endpointRegistry.Record(instanceBId);

            var period = HistoryPeriod.FromMinutes(MonitoredEndpointsModule.DefaultHistory);
            var now = DateTime.UtcNow.Subtract(new TimeSpan(period.IntervalSize.Ticks *  period.DelayedIntervals));

            var dataA = new RawMessage.Entry { DateTicks = now.Ticks, Value = 5 };
            var dataB = new RawMessage.Entry { DateTicks = now.Ticks, Value = 10 };

            processingTimeStore.Store(new[] { dataA }, instanceAId, EndpointMessageType.Unknown(instanceAId.EndpointName));
            processingTimeStore.Store(new[] { dataB }, instanceBId, EndpointMessageType.Unknown(instanceBId.EndpointName));

            var result = await queryAction(new
            {
                instanceAId.EndpointName
            }.ToDynamic(), new CancellationToken());

            var model = (MonitoredEndpointDetails)result.NegotiationContext.DefaultModel;


            Assert.AreEqual(5, model.Instances[0].Metrics["ProcessingTime"].Average);
        }

        static Func<dynamic, CancellationToken, Task<dynamic>> CreateQuery(ProcessingTimeStore processingTimeStore, EndpointRegistry endpointRegistry)
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

            var monitoredEndpointsModule = new MonitoredEndpointsModule(breakdownProviders, endpointRegistry, activityTracker, messageTypeRegistry)
            {
                Context = new NancyContext()
                {
                    Request = new Request("Get", "/monitored-endpoints/{endpointName}", "HTTP")
                }
            };

            var queryAction = monitoredEndpointsModule.Routes.Single(r => r.Description.Path == "/monitored-endpoints/{endpointName}").Action;

            return queryAction;
        }
    }
}