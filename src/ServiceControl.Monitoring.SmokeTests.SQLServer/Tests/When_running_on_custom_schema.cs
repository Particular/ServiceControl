namespace ServiceControl.Monitoring.SmokeTests.ASQ.Tests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;


    [Category("TransportSmokeTests")]
    public class When_running_on_custom_schema : ApiIntegrationTest
    {
        static string ReceiverEndpointName => NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(MonitoringEndpoint));
        const string CustomSchema = "nsb";

        [Test]
        public async Task Should_report_via_http()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<MonitoredEndpoint>(c =>
                {
                    c.When(s => s.SendLocal(new SampleMessage()));
                })
                .WithEndpoint<MonitoringEndpoint>()
                .Done(c => MetricReported("processingTime", out _, c))
                .Run();

            Assert.Pass("The monitoring instance is running on custom schema");
        }

        class MonitoredEndpoint : EndpointConfigurationBuilder
        {
            public MonitoredEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl($"{ReceiverEndpointName}@{CustomSchema}", TimeSpan.FromSeconds(1));
                });
            }

            class Handler : IHandleMessages<SampleMessage>
            {
                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }

        class MonitoringEndpoint : EndpointConfigurationBuilder
        {
            public MonitoringEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var connectionString = $"{DefaultServer.ConnectionString};Queue Schema={CustomSchema}";

                    EndpointFactory.MakeMetricsReceiver(c, Settings, connectionString);

                    c.LimitMessageProcessingConcurrencyTo(1);
                });
            }
        }
    }
}