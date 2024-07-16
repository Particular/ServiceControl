namespace ServiceControl.Monitoring.AcceptanceTests.Tests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Http.Diagrams;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

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

                    metricReported = result.HasResult && result.Items[0].Metrics.TryGetValue("processingTime", out var processingTime) && processingTime?.Average > 0;

                    return metricReported;
                })
                .Run();

            Assert.IsTrue(metricReported);
        }

        class EndpointWithTimings : EndpointConfigurationBuilder
        {
            public EndpointWithTimings() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1));
                });

            class Handler : IHandleMessages<SampleMessage>
            {
                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                    => Task.Delay(TimeSpan.FromMilliseconds(10), context.CancellationToken);
            }
        }

        class MonitoringEndpoint : EndpointConfigurationBuilder
        {
            public MonitoringEndpoint() => EndpointSetup<DefaultServerWithoutAudit>();
        }

        class Context : ScenarioContext;

        class SampleMessage : SampleBaseMessage;

        class SampleBaseMessage : IMessage;
    }
}