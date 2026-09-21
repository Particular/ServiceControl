namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

[TestFixture]
// Mandatory, not stylistic: this assembly is Parallelizable(ParallelScope.All) and these fixtures set
// process-global environment variables. One fixture added without it makes the whole suite intermittent.
[NonParallelizable]
class When_endpoint_settings_are_migrated : MigrationAcceptanceTest
{
    [Test]
    public async Task The_source_server_starts_and_creates_both_databases()
    {
        var source = await MigrationSourceServer.CreateDatabases();

        Assert.That(source.ServerUrl, Does.StartWith("http://localhost:"));
        Assert.That(source.PrimaryDatabase, Is.Not.Empty);
        Assert.That(source.ThroughputDatabase, Is.EqualTo($"{source.PrimaryDatabase}-throughput"));
    }

    [Test]
    public async Task They_are_readable_through_the_product_read_api()
    {
        await SeedSourceKnownEndpoints("Sales", "Billing");
        await SeedSourceEndpointSettings(("Sales", true), ("Billing", false), ("", true));

        // A clock that never moves keeps HeartbeatEndpointSettingsSyncHostedService's twenty second
        // delay from elapsing and rewriting these rows while the assertions read them.
        await RunHostUntilTheApiAnswers(builder => builder.Services.AddSingleton<TimeProvider>(new FakeTimeProvider()));

        var settings = await GetEndpointSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Single(row => row.Name == "Sales").TrackInstances, Is.True);
            Assert.That(settings.Single(row => row.Name == "Billing").TrackInstances, Is.False);
            Assert.That(settings.Single(row => row.Name == string.Empty).TrackInstances, Is.True);
        });
    }
}
