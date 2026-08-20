namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Monitoring;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_endpoint_tracking_is_configured : AcceptanceTest
    {
        [Test]
        public async Task Should_read_and_change_tracking_for_one_endpoint_and_for_the_default()
        {
            List<SettingsData> initial = null;
            List<SettingsData> afterEndpointChange = null;
            List<SettingsData> afterDefaultChange = null;

            await Define<Context>()
                .WithEndpoint<Tracked>()
                .Do("Read the settings the heartbeats page opens with", async _ =>
                {
                    initial = (await this.TryGetMany<SettingsData>("/api/endpointssettings")).Items;

                    return initial.Count > 0;
                })
                .Do("Stop tracking instances of the endpoint that redeploys", async _ =>
                {
                    await this.Patch($"/api/endpointssettings/{TrackedEndpoint}", new { track_instances = false });

                    var settings = await this.TryGetMany<SettingsData>("/api/endpointssettings",
                        setting => setting.Name == TrackedEndpoint && !setting.TrackInstances);

                    afterEndpointChange = settings.HasResult
                        ? (await this.TryGetMany<SettingsData>("/api/endpointssettings")).Items
                        : null;

                    return afterEndpointChange != null;
                })
                .Do("Change the default every other endpoint inherits", async _ =>
                {
                    await this.Patch("/api/endpointssettings", new { track_instances = false });

                    var settings = await this.TryGetMany<SettingsData>("/api/endpointssettings",
                        setting => setting.Name == string.Empty && !setting.TrackInstances);

                    afterDefaultChange = settings.HasResult
                        ? (await this.TryGetMany<SettingsData>("/api/endpointssettings")).Items
                        : null;

                    return afterDefaultChange != null;
                })
                .Done(_ => true)
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Default(initial)?.TrackInstances, Is.True,
                    "The page reads its default from the row with an empty name, so one has to be there before anything is saved");

                Assert.That(afterEndpointChange.Single(setting => setting.Name == TrackedEndpoint).TrackInstances, Is.False,
                    "Turning tracking off for one endpoint has to come back off");

                Assert.That(Default(afterEndpointChange), Is.Not.Null,
                    "The page reads the default row with a non-null assertion, so saving one endpoint must not take it away");

                Assert.That(Default(afterEndpointChange).TrackInstances, Is.True,
                    "Changing one endpoint is not changing the default");

                Assert.That(Default(afterDefaultChange).TrackInstances, Is.False);

                Assert.That(afterDefaultChange.Select(setting => setting.Name), Is.EquivalentTo(afterEndpointChange.Select(setting => setting.Name)),
                    "Changing the default edits the row that is already there rather than adding another");
            }
        }

        static SettingsData Default(IEnumerable<SettingsData> settings) =>
            settings.SingleOrDefault(setting => setting.Name == string.Empty);

        static string TrackedEndpoint => Conventions.EndpointNamingConvention(typeof(Tracked));

        class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }
        }

        public class Tracked : EndpointConfigurationBuilder
        {
            public Tracked() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.SendHeartbeatTo(Settings.DEFAULT_INSTANCE_NAME));
        }
    }
}
