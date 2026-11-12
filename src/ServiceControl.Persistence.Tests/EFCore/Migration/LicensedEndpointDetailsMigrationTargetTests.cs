namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;

class LicensedEndpointDetailsMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory LicensedEndpointDetailsCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.LicensedEndpointDetails);

    [Test]
    public async Task Count_is_the_licensed_endpoint_details_row_alone()
    {
        await LicensingDataStore.SaveReportMasks(["contoso"]);
        Assert.That(await Target.Count(LicensedEndpointDetailsCategory), Is.Zero, "the report masks row is not a licensed endpoint details row");

        await LicensingDataStore.SaveLicensedEndpointDetails(new LicensedEndpointDetails { LicenseId = "abc-123" });

        Assert.That(await Target.Count(LicensedEndpointDetailsCategory), Is.EqualTo(1), "counted as the whole Settings table, this would also count the report masks row and every other row in the table");
    }
}
