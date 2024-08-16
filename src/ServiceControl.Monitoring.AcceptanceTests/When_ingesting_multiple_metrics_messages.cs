namespace ServiceControl.Monitoring.AcceptanceTests.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Http.Diagrams;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NUnit.Framework;

    class When_ingesting_multiple_metrics_messages : AcceptanceTest
    {
        [Test]
        public async Task Should_not_fail()
        {
            CustomConfiguration = endpointConfiguration =>
            {
                endpointConfiguration.Pipeline.Register(typeof(InterceptIngestionBehavior),
                    "Intercepts ingestion exceptions");
            };

            var metricReported = false;

            var ctx = await Define<Context>()
                .WithEndpoint<EndpointWithTimings>(c => c.When(async s =>
                {
                    var tasks = new List<Task>();
                    for (int i = 0; i < 100; i++)
                    {
                        tasks.Add(s.SendLocal(new SampleMessage()));
                        tasks.Add(s.SendLocal(new AnotherSampleMessage()));
                    }

                    await Task.WhenAll(tasks);
                }))
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MonitoredEndpoint>("/monitored-endpoints?history=1");

                    metricReported = result.HasResult && result.Items[0].Metrics.TryGetValue("processingTime", out var processingTime) && processingTime?.Average > 0;

                    return metricReported;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(metricReported, Is.True);
                Assert.That(ctx.Errors, Is.Empty);
            });
        }

        class EndpointWithTimings : EndpointConfigurationBuilder
        {
            public EndpointWithTimings() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromSeconds(5));
                });

            class Handler : IHandleMessages<SampleMessage>
            {
                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                    => Task.Delay(TimeSpan.FromMilliseconds(10), context.CancellationToken);
            }

            class AnotherHandler : IHandleMessages<AnotherSampleMessage>
            {
                public Task Handle(AnotherSampleMessage message, IMessageHandlerContext context)
                    => Task.Delay(TimeSpan.FromMilliseconds(10), context.CancellationToken);
            }
        }

        class InterceptIngestionBehavior(ScenarioContext scenarioContext) : Behavior<IIncomingPhysicalMessageContext>
        {
            public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
            {
                try
                {
                    await next();
                }
                catch (Exception e)
                {
                    ((Context)scenarioContext).Errors.Add(e);
                    throw;
                }
            }
        }

        class MonitoringEndpoint : EndpointConfigurationBuilder
        {
            public MonitoringEndpoint() => EndpointSetup<DefaultServerWithoutAudit>();
        }

        class Context : ScenarioContext
        {
            public List<Exception> Errors { get; set; } = [];
        }

        class SampleMessage : SampleBaseMessage;

        class AnotherSampleMessage : SampleBaseMessage;

        class SampleBaseMessage : IMessage;
    }
}