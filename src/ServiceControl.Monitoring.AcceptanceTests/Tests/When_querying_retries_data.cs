namespace NServiceBus.Metrics.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using global::Newtonsoft.Json.Linq;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_querying_retries_data : ApiIntegrationTest
    {
        static string ReceiverEndpointName => Conventions.EndpointNamingConvention(typeof(MonitoringEndpoint));

        [Test]
        public async Task Should_report_via_http()
        {
            JToken retries = null;

            await Scenario.Define<Context>()
                .WithEndpoint<MonitoredEndpoint>(c =>
                {
                    c.DoNotFailOnErrorMessages();
                    c.CustomConfig(ec => ec.Recoverability().Immediate(i => i.NumberOfRetries(5)));
                    c.When(s => s.SendLocal(new SampleMessage()));
                })
                .WithEndpoint<MonitoringEndpoint>(c =>
                {
                    c.CustomConfig(conf =>
                    {
                        Bootstrapper.CreateReceiver(conf, ConnectionString);
                        Bootstrapper.StartWebApi();
                        conf.LimitMessageProcessingConcurrencyTo(1);
                    });
                })
                .Done(c => MetricReported("retries", out retries, c))
                .Run();

            Assert.IsTrue(retries["average"].Value<double>() > 0);
            Assert.AreEqual(60, retries["points"].Value<JArray>().Count);
        }

        class MonitoredEndpoint : EndpointConfigurationBuilder
        {
            public MonitoredEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(ReceiverEndpointName, TimeSpan.FromSeconds(5));
                });
            }

            class Handler : IHandleMessages<SampleMessage>
            {
                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    throw new Exception("Boom!");
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
    }
}