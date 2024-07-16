namespace ServiceControl.Monitoring.AcceptanceTests.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Metrics;
    using NUnit.Framework;

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
                        sendOptions.SetDestination(Settings.DEFAULT_INSTANCE_NAME);
                        sendOptions.SetHeader(MetricHeaders.MetricInstanceId, "MetricInstanceId");
                        return session.Send(new MetricReport { Data = "{}" }, sendOptions);
                    }))
                .Done(ctx => ctx.Logs.Any(x => x.Message == "Legacy queue length report received from MetricInstanceId instance of SendingLegacyMetricReport.EndpointSendingLegacyMetricReport"))
                .Run();
        }

        class EndpointSendingLegacyMetricReport : EndpointConfigurationBuilder
        {
            public EndpointSendingLegacyMetricReport() => EndpointSetup<DefaultServerWithoutAudit>();
        }

        class SomeContext : ScenarioContext;
    }
}