namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
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
                EndpointSetup<DefaultServerWithoutAudit>(c => c.SendHeartbeatTo(Settings.DEFAULT_SERVICE_NAME));
            }
        }
    }
}