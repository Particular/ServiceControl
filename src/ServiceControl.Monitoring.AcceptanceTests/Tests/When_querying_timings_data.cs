namespace NServiceBus.Metrics.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::ServiceControl.AcceptanceTesting;
    using global::ServiceControl.Monitoring.Http.Diagrams;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;

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
                    c.EnableMetrics().SendMetricDataToServiceControl(global::ServiceControl.Monitoring.Settings.DEFAULT_ENDPOINT_NAME, TimeSpan.FromSeconds(1));
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