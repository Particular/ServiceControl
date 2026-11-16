namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class TrialEndDateMigrationTests : MigrationAcceptanceTest
{
    static readonly MigrationCategory TrialEndDateCategory = MigrationCategoryRegistry.Find(MigrationCategoryIds.TrialEndDate)!;

    [Test]
    public async Task Copies_the_trial_end_date_as_one_row_in_settings()
    {
        var endDate = new DateOnly(2027, 3, 15);
        await SeedSourceTrialEndDate(endDate);
        Assert.That(await TrialLicenseDataProvider.GetTrialEndDate(), Is.Null, "the target holds no trial end date before the copy, so the date read afterwards came from the copy");

        var result = await CopyCategory("TrialEndDate");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(await TrialLicenseDataProvider.GetTrialEndDate(), Is.EqualTo(endDate), "the product's own trial licence store must read back the seeded date");
            Assert.That(await Target.Count(TrialEndDateCategory), Is.EqualTo(1), "--migration-verify reads this count");
        }
    }
}
