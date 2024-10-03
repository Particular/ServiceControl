namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Transports;

    [TestFixture]
    class RabbitMqTransportTests : TransportTestFixture
    {
        [TestCase(15)]
        [TestCase(null)]
        public async Task Should_set_max_concurrency_for_audit(int? setConcurrency)
        {
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = setConcurrency
            };

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<SendOnlyEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeAuditEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(transportSettings.MaxConcurrency == (setConcurrency ?? 32));
        }

        [TestCase(15)]
        [TestCase(null)]
        public async Task Should_set_max_concurrency_for_monitoring(int? setConcurrency)
        {
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = setConcurrency
            };

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<SendOnlyEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeMonitoringEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(transportSettings.MaxConcurrency == (setConcurrency ?? 32));
        }

        [TestCase(15)]
        [TestCase(null)]
        public async Task Should_set_max_concurrency_for_primary(int? setConcurrency)
        {
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = setConcurrency
            };

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<SendOnlyEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizePrimaryEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(transportSettings.MaxConcurrency == (setConcurrency ?? 10));
        }

        public class Context : ScenarioContext
        {
        }

        public class SendOnlyEndpoint : EndpointConfigurationBuilder
        {
            public SendOnlyEndpoint() => EndpointSetup<BasicEndpointSetup>();
        }
    }
}
