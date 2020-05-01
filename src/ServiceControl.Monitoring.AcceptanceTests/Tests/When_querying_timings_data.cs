namespace ServiceControl.Monitoring.AcceptanceTests.Tests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Http.Diagrams;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;

    class When_querying_timings_data : AcceptanceTest
    {

        [Test]
        public async Task Should_report_via_http()
        {
            var metricReported = false;

            await Define<Context>()
                .WithEndpoint<EndpointWithTimings>(c => c.When(s => s.SendLocal(new SampleMessage())))
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MonitoredEndpoint>("/monitored-endpoints?history=1");

                    metricReported = result.HasResult && result.Items[0].Metrics["processingTime"].Average > 0;

                    return metricReported;
                })
                .Run();

            Assert.IsTrue(metricReported);
        }

        class EndpointWithTimings : EndpointConfigurationBuilder
        {
            public EndpointWithTimings()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(Settings.DEFAULT_ENDPOINT_NAME, TimeSpan.FromSeconds(1));
                });
            }

            class Handler : IHandleMessages<SampleMessage>
            {
                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    return Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            }
        }

        class MonitoringEndpoint : EndpointConfigurationBuilder
        {
            public MonitoringEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        class Context : ScenarioContext
        {
        }

        class SampleMessage : IMessage
        {
        }
    }
}