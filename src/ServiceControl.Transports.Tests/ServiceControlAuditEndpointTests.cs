namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Transports;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    partial class ServiceControlAuditEndpointTests : FullEndpointTestFixture
    {
        [Test]
        public async Task Should_configure_audit_endpoint()
        {
            string endpointName = Conventions.EndpointNamingConvention(typeof(ServiceControlAuditEndpoint));
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1,
                EndpointName = endpointName
            };

            await configuration.TransportCustomization.ProvisionQueues(transportSettings, []);

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<ServiceControlAuditEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeAuditEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(ctx.EndpointsStarted, Is.True);
        }

        [TestCase(15)]
        [TestCase(null)]
        public async Task Should_set_max_concurrency(int? setConcurrency)
        {
            var endpointName = Conventions.EndpointNamingConvention(typeof(ServiceControlAuditEndpoint));
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                EndpointName = endpointName,
                MaxConcurrency = setConcurrency
            };

            //this shouldn't interfere in any way with the endpoint customisation
            await configuration.TransportCustomization.ProvisionQueues(new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                EndpointName = endpointName
            }, []);

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<ServiceControlAuditEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeAuditEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(ctx.EndpointsStarted, Is.True);
            Assert.That(transportSettings.MaxConcurrency, Is.EqualTo(setConcurrency ?? GetTransportDefaultConcurrency()));
        }

        private static partial int GetTransportDefaultConcurrency();

        public class Context : ScenarioContext;

        public class ServiceControlAuditEndpoint : EndpointConfigurationBuilder
        {
            public ServiceControlAuditEndpoint() =>
                EndpointSetup<BasicEndpointSetup>();
        }
    }
}