namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using CompositeViews.Endpoints;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceBus.Management.Infrastructure.Settings;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

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

            Assert.IsTrue(endpoint.Monitored);
            Assert.IsTrue(endpoint.IsSendingHeartbeats);
        }

        public class MyContext : ScenarioContext
        {
        }

        public class StartingEndpoint : EndpointConfigurationBuilder
        {
            public StartingEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.SendHeartbeatTo(Settings.DEFAULT_SERVICE_NAME));
            }
        }
    }
}