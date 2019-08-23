namespace NServiceBus.Metrics.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Features;
    using global::Newtonsoft.Json.Linq;
    using global::ServiceControl.Monitoring;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ServiceControl;

    [Category("Integration")]
    public class When_querying_queue_length_data : ApiIntegrationTest
    {
        static string ReceiverEndpointName => Conventions.EndpointNamingConvention(typeof(Receiver));
        static int ReporterQueueLengthValue = 10;

        [Test]
        public async Task When_sending_single_interval_data_Should_report_average_based_on_this_single_interval()
        {
            JToken queueLength = null;

            await Scenario.Define<Context>()
                .WithEndpoint<MonitoredEndpoint>()
                .WithEndpoint<Receiver>()
                .Done(c => MetricReported("queueLength", out queueLength, c))
                .Run();

            Assert.AreEqual(10, queueLength["average"].Value<int>());

            var points = queueLength["points"].Values<int>().ToArray();

            CollectionAssert.IsNotEmpty(points);

            Assert.IsTrue(points.All(v => v == ReporterQueueLengthValue || v == 0));
            Assert.IsTrue(points.Any(v => v == ReporterQueueLengthValue));
        }

        class MonitoredEndpoint : EndpointConfigurationBuilder
        {
            public MonitoredEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(ReceiverEndpointName, TimeSpan.FromSeconds(5));
                    c.EnableFeature<QueueLengthReporting>();
                });
            }

            public class QueueLengthReporting : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.RegisterStartupTask(b => new QueueLengthReportingTask(b.Build<IReportNativeQueueLength>()));
                }
            }

            public class QueueLengthReportingTask : FeatureStartupTask
            {
                IReportNativeQueueLength reporter;

                public QueueLengthReportingTask(IReportNativeQueueLength reporter)
                {
                    this.reporter = reporter;
                }

                protected override Task OnStart(IMessageSession session)
                {
                    reporter.ReportQueueLength(reporter.MonitoredQueues.First(), ReporterQueueLengthValue);

                    return TaskEx.Completed;
                }

                protected override Task OnStop(IMessageSession session)
                {
                    return TaskEx.Completed;
                }
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    EndpointFactory.MakeMetricsReceiver(c, Settings, ConnectionString);
                    c.LimitMessageProcessingConcurrencyTo(1);
                });
            }
        }
    }
}