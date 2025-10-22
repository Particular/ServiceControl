namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts.EndpointControl;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [TestFixture]
    class When_unmonitored_endpoint_starts_to_sends_heartbeats : AcceptanceTest
    {
        static string EndpointName => Conventions.EndpointNamingConvention(typeof(WithoutHeartbeat));

        [Test]
        public async Task Should_be_marked_as_monitored()
        {
            EndpointsView endpoint = null;

            await Define<MyContext>()
                .WithEndpoint<WithoutHeartbeat>(c => c.When(bus =>
                {
                    var options = new SendOptions();

                    options.DoNotEnforceBestPractices();
                    options.SetDestination(PrimaryOptions.DEFAULT_INSTANCE_NAME);

                    return bus.Send(new NewEndpointDetected
                    {
                        Endpoint = new EndpointDetails
                        {
                            HostId = Guid.NewGuid(),
                            Host = "myhost",
                            Name = EndpointName
                        },
                        DetectedAt = DateTime.UtcNow
                    }, options);
                }))
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EndpointsView>("/api/endpoints", e => e.Name == EndpointName);

                    endpoint = result.Item;

                    return result.HasResult;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(endpoint.MonitorHeartbeat, Is.False);
                Assert.That(endpoint.Monitored, Is.False);
            });

            await Define<MyContext>()
                .WithEndpoint<WithHeartbeat>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EndpointsView>("/api/endpoints", e => e.Name == EndpointName && e.IsSendingHeartbeats);

                    endpoint = result.Item;

                    return result.HasResult;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(endpoint.MonitorHeartbeat, Is.True, "Should have heartbeat monitoring on");
                Assert.That(endpoint.Monitored, Is.True, "Should be flagged as monitored");
                Assert.That(endpoint.IsSendingHeartbeats, Is.True, "Should be emitting heartbeats");
            });
        }

        public class MyContext : ScenarioContext;

        public class WithoutHeartbeat : EndpointConfigurationBuilder
        {
            public WithoutHeartbeat() => EndpointSetup<DefaultServerWithoutAudit>();
        }

        public class WithHeartbeat : EndpointConfigurationBuilder
        {
            public WithHeartbeat() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.SendHeartbeatTo(PrimaryOptions.DEFAULT_INSTANCE_NAME); }).CustomEndpointName(EndpointName);
        }
    }
}