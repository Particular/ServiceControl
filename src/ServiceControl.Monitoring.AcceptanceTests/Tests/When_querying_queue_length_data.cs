namespace NServiceBus.Metrics.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using global::Newtonsoft.Json.Linq;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_querying_queue_length_data : ApiIntegrationTest
    {
        static string MonitoringEndpointName => Conventions.EndpointNamingConvention(typeof(MonitoringEndpoint));

        [Test]
        public async Task Should_report_via_http()
        {
            JToken queueLength = null;

            await Scenario.Define<QueueLengthContext>()
                .WithEndpoint<SendingEndpoint>(c =>
                {
                    c.DoNotFailOnErrorMessages();
                    c.When(async s =>
                    {
                        await s.SendLocal(new SampleMessage());
                        await s.SendLocal(new SampleMessage());
                        await s.SendLocal(new SampleMessage());
                    });
                })
                .WithEndpoint<MonitoringEndpoint>(c =>
                {
                    c.CustomConfig(conf =>
                    {
                        Bootstrapper.CreateReceiver(conf, ConnectionString);
                        Bootstrapper.StartWebApi();
                    });
                })
                .Done(c =>
                {
                    var done = MetricReported("queueLength", out queueLength, c);

                    if (done)
                    {
                        c.CancelProcessingTokenSource.Cancel();
                    }

                    return done;
                })
                .Run();

            Assert.IsTrue(queueLength["average"].Value<double>() > 0);
            Assert.AreEqual(60, queueLength["points"].Value<JArray>().Count);
        }

        class SendingEndpoint : EndpointConfigurationBuilder
        {
            public SendingEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(MonitoringEndpointName, TimeSpan.FromSeconds(1));
                    c.LimitMessageProcessingConcurrencyTo(1);
                });
            }

            class Handler : IHandleMessages<SampleMessage>
            {
                public QueueLengthContext Context { get; set; }

                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    //Concurrency limit 1 and this should block any processing on input queue
                    return Task.Delay(TimeSpan.FromSeconds(30), Context.CancelProcessingTokenSource.Token);
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

        class QueueLengthContext : Context
        {
            public CancellationTokenSource CancelProcessingTokenSource = new CancellationTokenSource();
        }
    }
}