namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ServiceControl.Persistence;

[TestFixture]
class TrialLicenseDataProviderTests
{
    [Test]
    public async Task Should_store_and_retrieve_trial_end_date()
    {
        var context = new PersistenceTestsContext();
        var hostBuilder = Host.CreateApplicationBuilder();

        await context.Setup(hostBuilder);

        using var host = hostBuilder.Build();
        await host.StartAsync();

        await context.PostSetup(host);

        var provider = host.Services.GetRequiredService<ITrialLicenseDataProvider>();

        // Initially should be null
        var initialValue = await provider.GetTrialEndDate(default);
        Assert.That(initialValue, Is.Null);

        // Store a trial end date
        var expectedDate = new DateOnly(2025, 12, 31);
        await provider.StoreTrialEndDate(expectedDate, default);

        // Retrieve and verify
        var retrievedDate = await provider.GetTrialEndDate(default);
        Assert.That(retrievedDate, Is.EqualTo(expectedDate));

        // Update the trial end date
        var updatedDate = new DateOnly(2026, 6, 30);
        await provider.StoreTrialEndDate(updatedDate, default);

        // Retrieve and verify update
        var retrievedUpdatedDate = await provider.GetTrialEndDate(default);
        Assert.That(retrievedUpdatedDate, Is.EqualTo(updatedDate));

        await host.StopAsync();
        await context.TearDown();
    }
}
