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

    class When_querying_retries_data : AcceptanceTest
    {
        [Test]
        public async Task Should_report_via_http()
        {
            var metricReported = false;

            await Define<TestContext>()
                .WithEndpoint<EndpointWithRetries>(c =>
                {
                    c.DoNotFailOnErrorMessages();
                    c.CustomConfig(ec => ec.Recoverability().Immediate(i => i.NumberOfRetries(5)));
                    c.When(s => s.SendLocal(new SampleMessage()));
                })
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MonitoredEndpoint>("/monitored-endpoints?history=1");

                    metricReported = result.HasResult && result.Items[0].Metrics["retries"].Average > 0;

                    if (metricReported)
                    {
                        c.ShuttingDown = true;
                    }

                    return metricReported;
                })
                .Run();

            Assert.IsTrue(metricReported);
        }

        class EndpointWithRetries : EndpointConfigurationBuilder
        {
            public EndpointWithRetries()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(global::ServiceControl.Monitoring.Settings.DEFAULT_ENDPOINT_NAME, TimeSpan.FromSeconds(1));
                });
            }

            class Handler : IHandleMessages<SampleMessage>
            {
                TestContext testContext;

                public Handler(TestContext testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    if (testContext.ShuttingDown)
                    {
                        return Task.CompletedTask;
                    }

                    throw new Exception("Boom!");
                }
            }
        }

        class TestContext : ScenarioContext
        {
            public bool ShuttingDown { get; set; }
        }

        class SampleMessage : IMessage
        {
        }
    }
}