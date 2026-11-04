namespace ServiceControl.Migration.AcceptanceTests;

using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class LicensingSettingsMigrationTests : MigrationAcceptanceTest
{
    static LicensedEndpointDetails Details => new LicensedEndpointDetails
    {
        LicenseId = "abc-123",
        ValidId = true,
        ServiceEndDate = "2027-01-01",
        Endpoints = [new LicensedEndpoint { Name = "Sales.Orders", Classification = 2, EndpointSize = "Large", Queues = [new QueueIdentity { NameHash = "n1", ScopeHash = "s1" }] }],
        InfrastructureQueues = [new QueueIdentity { NameHash = "n2", ScopeHash = "s2" }],
        ExcludedQueues = [new QueueIdentity { NameHash = "n3", ScopeHash = "s3" }],
        Products = [new Product { ProductCode = "P1", MonthlyThroughput = 500 }]
    };

    [Test]
    public async Task Copies_report_masks_as_one_row_in_settings()
    {
        await SeedThroughputReportMasks(["contoso", "fabrikam"]);

        var result = await CopyCategory("LicensingReportMasks");

        Assert.That(result.Copied, Is.EqualTo(1));
        var masks = await TargetLicensingDataStore.GetReportMasks();
        Assert.That(masks, Is.EquivalentTo(new[] { "contoso", "fabrikam" }));
    }

    [Test]
    public async Task Copies_the_licensed_endpoint_details_file_as_one_row_in_settings()
    {
        var seeded = Details;
        await SeedThroughputLicensedEndpointDetails(seeded);

        var result = await CopyCategory("LicensedEndpointDetails");

        Assert.That(result.Copied, Is.EqualTo(1));
        var details = await TargetLicensingDataStore.GetLicensedEndpointDetails();
        Assert.That(JsonSerializer.Serialize(details), Is.EqualTo(JsonSerializer.Serialize(seeded)), "every field, including the endpoint, queue and product arrays, must reach the SQL row");
    }

    [Test]
    public async Task Report_masks_an_earlier_pass_copied_are_kept_and_reported_as_already_present()
    {
        await TargetLicensingDataStore.SaveReportMasks(["copied earlier"]);
        await SeedThroughputReportMasks(["contoso"]);

        var result = await CopyCategory("LicensingReportMasks");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
            Assert.That(await TargetLicensingDataStore.GetReportMasks(), Is.EquivalentTo(new[] { "copied earlier" }), "a copy inserts a settings row only where the target has none, so a value an earlier pass copied is never overwritten");
        }
    }

    [Test]
    public async Task Licensed_endpoint_details_an_earlier_pass_copied_are_kept_and_reported_as_already_present()
    {
        await TargetLicensingDataStore.SaveLicensedEndpointDetails(new LicensedEndpointDetails { LicenseId = "copied earlier" });
        await SeedThroughputLicensedEndpointDetails(new LicensedEndpointDetails { LicenseId = "abc-123" });

        var result = await CopyCategory("LicensedEndpointDetails");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
            Assert.That((await TargetLicensingDataStore.GetLicensedEndpointDetails())!.LicenseId, Is.EqualTo("copied earlier"), "a copy inserts a settings row only where the target has none, so a value an earlier pass copied is never overwritten");
        }
    }

    [Test]
    public async Task An_absent_document_writes_no_row()
    {
        var masks = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingReportMasks");
        var details = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensedEndpointDetails");

        var masksResult = await CopyCategory(masks.Id);
        var detailsResult = await CopyCategory(details.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(masksResult.Copied + detailsResult.Copied, Is.Zero);
            Assert.That(await Target.Count(masks), Is.Zero, "an absent RavenDB document hands over nothing, so no empty masking list is invented on the SQL side");
            Assert.That(await Target.Count(details), Is.Zero);
        }
    }
}
