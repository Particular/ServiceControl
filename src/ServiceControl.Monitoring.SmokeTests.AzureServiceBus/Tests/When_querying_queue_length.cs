namespace ServiceControl.Monitoring.SmokeTests.AzureServiceBus.Tests
{
    using System;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [Category("TransportSmokeTests")]
    public class When_querying_queue_length : ApiIntegrationTest
    {
        static string MonitoringEndpointName => Conventions.EndpointNamingConvention(typeof(MonitoringEndpoint));

        [Test]
        public async Task Should_report_via_http()
        {
            JToken metric = null;

            await Scenario.Define<QueueLengthContext>()
                .WithEndpoint<MonitoringEndpoint>()
                .WithEndpoint<MonitoredEndpoint>(c => c.When(async s =>
                {
                    await s.SendLocal(new SampleMessage());
                    await s.SendLocal(new SampleMessage());
                    await s.SendLocal(new SampleMessage());
                }))
                .Done(c =>
                {
                    c.Done = MetricReported("queueLength", out metric, c);

                    return c.Done;
                })
                .Run();

            Assert.IsTrue(metric["average"].Value<double>() > 0);
            Assert.AreEqual(60, metric["points"].Value<JArray>().Count);
        }

        class MonitoredEndpoint : EndpointConfigurationBuilder
        {
            public MonitoredEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(MonitoringEndpointName, TimeSpan.FromSeconds(1));
                    c.LimitMessageProcessingConcurrencyTo(1);
                });
            }

            class Handler : IHandleMessages<SampleMessage>
            {
                public QueueLengthContext QueueLengthContext { get; set; }

                public async Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    while (!QueueLengthContext.Done)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
                    }
                }
            }
        }

        class MonitoringEndpoint : EndpointConfigurationBuilder
        {
            public MonitoringEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    EndpointFactory.MakeMetricsReceiver(c, Settings, DefaultServer.ConnectionString);
                });
            }
        }

        class QueueLengthContext : Context
        {
            public bool Done { get; set; }
        }
    }
}
