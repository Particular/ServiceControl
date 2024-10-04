namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Transports;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    partial class ServiceControlMonitoringEndpointTests : FullEndpointTestFixture
    {
        [Test]
        public async Task Should_configure_monitoring_endpoint()
        {
            string endpointName = Conventions.EndpointNamingConvention(typeof(ServiceControlMonitoringEndpoint));
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1,
                EndpointName = endpointName
            };

            await configuration.TransportCustomization.ProvisionQueues(transportSettings, []);

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<ServiceControlMonitoringEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeMonitoringEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(ctx.EndpointsStarted, Is.True);
        }

        [TestCase(15)]
        [TestCase(null)]
        public async Task Should_set_max_concurrency(int? setConcurrency)
        {
            var endpointName = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ServiceControlMonitoringEndpoint));
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                EndpointName = endpointName
            };

            await configuration.TransportCustomization.ProvisionQueues(new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                EndpointName = endpointName
            }, []);

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<ServiceControlMonitoringEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeMonitoringEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(ctx.EndpointsStarted, Is.True);
            Assert.That(transportSettings.MaxConcurrency == (setConcurrency ?? GetTransportDefaultConcurrency()));
        }

        private partial int GetTransportDefaultConcurrency();

        public class Context : ScenarioContext;

        public class ServiceControlMonitoringEndpoint : EndpointConfigurationBuilder
        {
            public ServiceControlMonitoringEndpoint()
            {
                EndpointSetup<BasicEndpointSetup>(c =>
                {
                    c.UsePersistence<NonDurablePersistence>();
                });
            }
        }
    }
}