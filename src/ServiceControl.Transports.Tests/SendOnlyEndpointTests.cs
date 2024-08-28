namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Transports;

    class SendOnlyEndpointTests : FullEndpointTestFixture
    {
        [Test]
        public async Task Should_be_able_to_create_send_only_endpoint()
        {
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1
            };

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<SendOnlyEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeAuditEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(ctx.EndpointsStarted, Is.True);
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