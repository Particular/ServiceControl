namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;

class LicensingReportMasksMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory LicensingReportMasksCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.LicensingReportMasks);

    [Test]
    public async Task Count_is_the_report_masks_row_alone()
    {
        await LicensingDataStore.SaveLicensedEndpointDetails(new LicensedEndpointDetails { LicenseId = "abc-123" });
        Assert.That(await Target.Count(LicensingReportMasksCategory), Is.Zero, "the licensed endpoint details row is not a report masks row");

        await LicensingDataStore.SaveReportMasks(["contoso"]);

        Assert.That(await Target.Count(LicensingReportMasksCategory), Is.EqualTo(1), "counted as the whole Settings table, this would also count the licensed endpoint details row and every other row in the table");
    }
}
