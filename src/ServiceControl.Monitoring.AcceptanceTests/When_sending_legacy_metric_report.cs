namespace ServiceControl.Monitoring.AcceptanceTests.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Metrics;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;

    class When_sending_legacy_metric_report : AcceptanceTest
    {
        [Test]
        public async Task Should_report_legacy_queue_length_reporting()
        {
            await Define<SomeContext>()
                .WithEndpoint<EndpointSendingLegacyMetricReport>(b =>
                    b.When(session =>
                    {
                        var sendOptions = new SendOptions();
                        sendOptions.SetDestination(Settings.DEFAULT_ENDPOINT_NAME);
                        sendOptions.SetHeader(MetricHeaders.MetricInstanceId, "MetricInstanceId");
                        return session.Send(new MetricReport(), sendOptions);
                    }))
                .Done(ctx => ctx.Logs.Any(x => x.Message == "Legacy queue length report received from MetricInstanceId instance of SendingLegacyMetricReport.EndpointSendingLegacyMetricReport"))
                .Run();
        }

        class EndpointSendingLegacyMetricReport : EndpointConfigurationBuilder
        {
            public EndpointSendingLegacyMetricReport()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        class SomeContext : ScenarioContext
        {

        }
    }
}