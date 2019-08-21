namespace ServiceControl.Monitoring.SmokeTests.AzureStorageQueues.Tests
{
    using System;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Conventions = NServiceBus.AcceptanceTesting.Customization;

    [Category("TransportSmokeTests")]
    public class When_querying_timings_data : ApiIntegrationTest
    {
        static string ReceiverEndpointName => Conventions.Conventions.EndpointNamingConvention(typeof(MonitoringEndpoint));

        [Test]
        public async Task Should_report_via_http()
        {
            JToken processingTime = null;

            await Scenario.Define<Context>()
                    .WithEndpoint<MonitoredEndpoint>(c => c.When(s => s.SendLocal(new SampleMessage())))
                    .WithEndpoint<MonitoringEndpoint>()
                    .Done(c => MetricReported("processingTime", out processingTime, c))
                    .Run();

            Assert.IsTrue(processingTime["average"].Value<int>() > 0);
            Assert.AreEqual(60, processingTime["points"].Value<JArray>().Count);
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
                    return Task.Delay(TimeSpan.FromMilliseconds(10));
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
                    c.LimitMessageProcessingConcurrencyTo(1);
                });
            }
        }
    }
}