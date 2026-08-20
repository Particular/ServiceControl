namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;

    class When_heartbeat_stats_are_requested : AcceptanceTest
    {
        [Test]
        public async Task Should_count_a_heartbeating_endpoint_as_active()
        {
            HeartbeatStats stats = null;

            await Define<Context>()
                .WithEndpoint<HeartbeatingEndpoint>()
                .Done(async _ =>
                {
                    var result = await this.TryGet<HeartbeatStats>("/api/heartbeats/stats", found => found.Active > 0);
                    stats = result.Item;
                    return result.HasResult;
                })
                .Run();

            Assert.That(stats.Failing, Is.Zero,
                "An endpoint still sending heartbeats must not also be counted against the failing tile");
        }

        [Test]
        public async Task Should_count_an_endpoint_past_its_grace_period_as_failing()
        {
            HeartbeatStats stats = null;

            // Short enough that the endpoint below is past it by the time the monitor first looks,
            // however recently it last reported.
            SetSettings = settings => settings.HeartbeatGracePeriod = TimeSpan.FromMilliseconds(1);

            await Define<Context>()
                .WithEndpoint<HeartbeatingEndpoint>()
                .Done(async _ =>
                {
                    var result = await this.TryGet<HeartbeatStats>("/api/heartbeats/stats", found => found.Failing > 0);
                    stats = result.Item;
                    return result.HasResult;
                })
                .Run();

            Assert.That(stats.Active, Is.Zero,
                "An endpoint counted as failing must have left the active tile, or the two tiles double count it");
        }

        record HeartbeatStats(int Active, int Failing);

        class Context : ScenarioContext;

        public class HeartbeatingEndpoint : EndpointConfigurationBuilder
        {
            public HeartbeatingEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.SendHeartbeatTo(Settings.DEFAULT_INSTANCE_NAME));
        }
    }
}
