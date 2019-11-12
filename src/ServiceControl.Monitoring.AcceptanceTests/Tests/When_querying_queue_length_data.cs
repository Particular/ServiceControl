namespace NServiceBus.Metrics.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::ServiceControl.AcceptanceTests;
    using global::ServiceControl.Monitoring.Http.Diagrams;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using AcceptanceTest = ServiceBus.Management.AcceptanceTests.AcceptanceTest;

    [RunOnAllTransports]
    class When_querying_queue_length_data : AcceptanceTest
    {
        [Test]
        public async Task Should_report_via_http()
        {
            var metricReported = false;

            await Define<QueueLengthContext>()
                .WithEndpoint<SendingEndpoint>(c =>
                {
                    c.DoNotFailOnErrorMessages();
                    c.When(async s =>
                    {
                        for (var i = 0; i < 10; i++)
                        {
                            await s.SendLocal(new SampleMessage());
                        }
                    });
                })
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MonitoredEndpoint>("/monitored-endpoints?history=1");

                    metricReported = result.HasResult && result.Items[0].Metrics["queueLength"].Average > 0;

                    if (metricReported)
                    {
                        c.TestEnded.SetResult(true);
                    }

                    return metricReported;
                })
                .Run();


            Assert.IsTrue(metricReported);
        }

        class SendingEndpoint : EndpointConfigurationBuilder
        {
            public SendingEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(global::ServiceControl.Monitoring.Settings.DEFAULT_ENDPOINT_NAME, TimeSpan.FromSeconds(1));
                    c.LimitMessageProcessingConcurrencyTo(1);
                });
            }

            class Handler : IHandleMessages<SampleMessage>
            {
                public QueueLengthContext Context { get; set; }

                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    //Concurrency limit 1 and this should block any processing on input queue
                    return Task.WhenAny(
                        Task.Delay(TimeSpan.FromSeconds(30)), 
                            Context.TestEnded.Task
                        );
                }
            }
        }

        public class SampleMessage : IMessage
        {
        }

        public class QueueLengthContext : ScenarioContext
        {
            public TaskCompletionSource<bool> TestEnded = new TaskCompletionSource<bool>();
        }
    }
}