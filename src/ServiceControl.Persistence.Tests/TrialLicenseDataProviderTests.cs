namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence;

class TrialLicenseDataProviderTests : PersistenceTestBase
{
    [Test]
    public async Task GetTrialEndDate_returns_null_by_default()
    {
        var trialLicenseDataProvider = ServiceProvider.GetRequiredService<ITrialLicenseDataProvider>();

        var trialEndDate = await trialLicenseDataProvider.GetTrialEndDate(default);

        Assert.That(trialEndDate, Is.Null);
    }

    [Test]
    public async Task StoreTrialEndDate_persists_value()
    {
        var trialLicenseDataProvider = ServiceProvider.GetRequiredService<ITrialLicenseDataProvider>();
        var expectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(13));

        await trialLicenseDataProvider.StoreTrialEndDate(expectedEndDate, default);

        var trialEndDate = await trialLicenseDataProvider.GetTrialEndDate(default);

        Assert.That(trialEndDate, Is.EqualTo(expectedEndDate));
    }
}
