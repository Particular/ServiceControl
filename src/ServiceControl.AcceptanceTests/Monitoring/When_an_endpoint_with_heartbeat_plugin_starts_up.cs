namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [TestFixture]
    class When_an_endpoint_with_heartbeat_plugin_starts_up : AcceptanceTest
    {
        static string EndpointName => Conventions.EndpointNamingConvention(typeof(StartingEndpoint));

        [Test]
        public async Task Should_be_monitored_and_active()
        {
            EndpointsView endpoint = null;

            await Define<MyContext>()
                .WithEndpoint<StartingEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EndpointsView>("/api/endpoints/", e => e.Name == EndpointName && e.Monitored && e.MonitorHeartbeat && e.IsSendingHeartbeats);
                    endpoint = result.Item;
                    return result.HasResult;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(endpoint.Monitored, Is.True);
                Assert.That(endpoint.IsSendingHeartbeats, Is.True);
            });
        }

        [Test]
        public async Task Should_be_persisted()
        {
            var endpointName = Conventions.EndpointNamingConvention(typeof(StartingEndpoint));
            KnownEndpoint endpoint = default;

            await Define<MyContext>()
                .WithEndpoint<StartingEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<KnownEndpoint>("/api/test/knownendpoints/query",
                        e => e.EndpointDetails.Name == endpointName);
                    endpoint = result;
                    return result.HasResult;
                })
                .Run();

            Assert.That(endpoint.Monitored, Is.True, "An endpoint discovered from heartbeats should be monitored");
        }

        public class MyContext : ScenarioContext;

        public class StartingEndpoint : EndpointConfigurationBuilder
        {
            public StartingEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => c.SendHeartbeatTo(PrimaryOptions.DEFAULT_INSTANCE_NAME));
        }
    }
}