namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence;

class TrialLicenseDataProviderTests : PersistenceTestBase
{
    [Test, CancelAfter(10_000)]
    public async Task GetTrialEndDate_returns_null_by_default(CancellationToken cancellationToken)
    {
        var trialLicenseDataProvider = ServiceProvider.GetRequiredService<ITrialLicenseDataProvider>();

        var trialEndDate = await trialLicenseDataProvider.GetTrialEndDate(cancellationToken);

        Assert.That(trialEndDate, Is.Null);
    }

    [Test, CancelAfter(10_000)]
    public async Task StoreTrialEndDate_persists_value(CancellationToken cancellationToken)
    {
        var trialLicenseDataProvider = ServiceProvider.GetRequiredService<ITrialLicenseDataProvider>();
        var expectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(13));

        await trialLicenseDataProvider.StoreTrialEndDate(expectedEndDate, cancellationToken);

        var trialEndDate = await trialLicenseDataProvider.GetTrialEndDate(cancellationToken);

        Assert.That(trialEndDate, Is.EqualTo(expectedEndDate));
    }
}
