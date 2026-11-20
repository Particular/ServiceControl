namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;

class CustomCheckSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Reads_a_custom_check_as_the_neutral_detail_type_with_its_id_recoverable()
    {
        var endpoint = new EndpointDetails { Name = "Sales", Host = "MACHINE", HostId = Guid.NewGuid() };
        var expectedId = await SeedCustomCheck(endpoint, "Sales.HealthCheck", "Ping", failed: true, reason: "timed out");
        await CompleteDatabaseOperation();

        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "CustomChecks");

        await using var source = await OpenMigrationSource();

        var detail = (CustomCheckDetail)(await CollectBatches(source, category)).SelectMany(batch => batch.Rows).Single().Document;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(detail.GetDeterministicId(), Is.EqualTo(expectedId), "the id must be recoverable from the same fields RavenDB used to generate it");
            Assert.That(detail.CustomCheckId, Is.EqualTo("Sales.HealthCheck"));
            Assert.That(detail.HasFailed, Is.True);
            Assert.That(detail.FailureReason, Is.EqualTo("timed out"));
        }
    }

    [Test]
    public async Task Flags_only_a_row_whose_id_and_category_are_one_of_the_persisters_own_checks()
    {
        var endpoint = new EndpointDetails { Name = "Sales", Host = "MACHINE", HostId = Guid.NewGuid() };
        await SeedCustomCheck(endpoint, "Error Database Index Lag", "ServiceControl Health", failed: false, reason: null);
        await SeedCustomCheck(new EndpointDetails { Name = "Billing", Host = "MACHINE", HostId = Guid.NewGuid() }, "Error Database Index Lag", "Another category", failed: false, reason: null);
        await SeedCustomCheck(endpoint, "Sales.HealthCheck", "Ping", failed: false, reason: null);
        await SeedCustomCheck(endpoint, "Error Message Ingestion Process", "ServiceControl Health", failed: false, reason: null);
        await CompleteDatabaseOperation();

        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "CustomChecks");

        await using var source = await OpenMigrationSource();

        var rows = (await CollectBatches(source, category)).SelectMany(batch => batch.Rows).ToDictionary(row => ((CustomCheckDetail)row.Document).CustomCheckId + "|" + ((CustomCheckDetail)row.Document).Category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows["Error Database Index Lag|ServiceControl Health"].Metadata[MigrationMetadataKeys.SourceStorageCheck], Is.True);
            Assert.That(rows["Error Database Index Lag|Another category"].Metadata, Does.Not.ContainKey(MigrationMetadataKeys.SourceStorageCheck), "the same id under another category is not the persister's check");
            Assert.That(rows["Error Message Ingestion Process|ServiceControl Health"].Metadata, Does.Not.ContainKey(MigrationMetadataKeys.SourceStorageCheck), "another id under the persister's category is not the persister's check");
            Assert.That(rows["Sales.HealthCheck|Ping"].Metadata, Does.Not.ContainKey(MigrationMetadataKeys.SourceStorageCheck));
        }
    }

    async Task<Guid> SeedCustomCheck(EndpointDetails endpoint, string customCheckId, string category, bool failed, string reason)
    {
        var detail = new CustomCheckDetail
        {
            OriginatingEndpoint = endpoint,
            CustomCheckId = customCheckId,
            Category = category,
            ReportedAt = Now,
            HasFailed = failed,
            FailureReason = reason
        };

        await CustomChecks.UpdateCustomCheckStatus(detail);

        return detail.GetDeterministicId();
    }
}
