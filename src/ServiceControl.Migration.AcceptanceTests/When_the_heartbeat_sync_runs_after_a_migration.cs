namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence;

[TestFixture]
[NonParallelizable]
class When_the_heartbeat_sync_runs_after_a_migration : MigrationAcceptanceTest
{
    [Test]
    public async Task Migrated_endpoint_settings_survive_the_sync_under_the_name_their_known_endpoint_uses()
    {
        var time = new FakeTimeProvider();

        var billing = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = "Billing", HostId = Guid.NewGuid(), Host = "BILLING01" }, Monitored = true };

        await SeedSource(($"KnownEndpoints/{billing.EndpointDetails.GetDeterministicId()}", billing));
        await SeedSourceKnownEndpoints("SmokeSales");
        await SeedSourceEndpointSettings(("Billing", true), ("smokesales", true), ("SmokeSales", false));

        await RunRequiredCopy(customize: services =>
        {
            services.AddSingleton<TimeProvider>(time);
            CountSyncPasses(services);
        });

        time.Advance(TimeSpan.FromSeconds(21));
        await WaitForSyncPass(time);

        var settings = await EndpointSettingsStore.GetAllEndpointSettings().ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Select(setting => setting.Name), Does.Contain(string.Empty),
                "The heartbeat sync did not create the default row, which it does after its delete loop. The pass did not finish, so the other checks prove nothing.");
            Assert.That(settings.Select(setting => setting.Name), Does.Contain("Billing"),
                "The heartbeat sync deleted the migrated Billing setting. It only does that when no Billing known endpoint was copied.");
            Assert.That(settings.Where(setting => setting.Name == "SmokeSales").Select(setting => setting.TrackInstances), Is.EqualTo(new[] { false }),
                "SmokeSales must keep the operator's Do Not Track. On a target whose name column ignores case, the two settings become one row. If the copy keeps 'smokesales', the sync deletes it and SmokeSales gets the default.");
        }
    }
}
