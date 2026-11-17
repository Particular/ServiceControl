namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
[NonParallelizable]
class CustomCheckMigrationTests : MigrationAcceptanceTest
{
    static readonly EndpointDetails Endpoint = new() { Name = "Sales", Host = "MACHINE", HostId = Guid.NewGuid() };

    static Guid IdOf(EndpointDetails endpoint, string customCheckId) =>
        new CustomCheckDetail { OriginatingEndpoint = endpoint, CustomCheckId = customCheckId, Category = "Ping" }.GetDeterministicId();

    async Task<IList<CustomCheckView>> CopiedChecks() => (await CustomChecks.GetStats(new PagingInfo())).Results!;

    // One of each status, because Status.Fail is the enum's zero and a status that never deserializes would read as Fail.
    [Test]
    public async Task Copies_a_failing_and_a_passing_custom_check_with_their_deterministic_ids_and_reported_status()
    {
        await SeedSourceCustomCheck(Endpoint, "Sales.HealthCheck", "Ping", failed: true, reason: "timed out");
        await SeedSourceCustomCheck(Endpoint, "Sales.DiskCheck", "Disk", failed: false);

        var result = await CopyCategory("CustomChecks");

        var copied = await CopiedChecks();
        var failing = copied.Single(check => check.CustomCheckId == "Sales.HealthCheck");
        var passing = copied.Single(check => check.CustomCheckId == "Sales.DiskCheck");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(2));
            Assert.That(await Target.Count(MigrationCategoryRegistry.Find(MigrationCategoryIds.CustomChecks)!), Is.EqualTo(2), "Count is what --migration-verify reads");
            Assert.That(Guid.Parse(failing.Id!), Is.EqualTo(IdOf(Endpoint, "Sales.HealthCheck")), "a live report of the same check upserts on this id, so it replaces the copied row rather than adding a second");
            Assert.That(failing.Status, Is.EqualTo(Status.Fail));
            Assert.That(failing.Category, Is.EqualTo("Ping"));
            Assert.That(failing.FailureReason, Is.EqualTo("timed out"));
            Assert.That(failing.ReportedAt, Is.EqualTo(SeededReportedAt));
            Assert.That((failing.OriginatingEndpoint!.Name, failing.OriginatingEndpoint.Host, failing.OriginatingEndpoint.HostId), Is.EqualTo((Endpoint.Name, Endpoint.Host, Endpoint.HostId)));
            Assert.That(Guid.Parse(passing.Id!), Is.EqualTo(IdOf(Endpoint, "Sales.DiskCheck")));
            Assert.That(passing.Status, Is.EqualTo(Status.Pass), "a status read back as the enum's zero would show every copied check red");
            Assert.That(passing.Category, Is.EqualTo("Disk"));
            Assert.That(passing.FailureReason, Is.Null);
        }
    }

    [Test]
    public async Task A_check_with_no_originating_endpoint_is_skipped_as_a_required_value_missing()
    {
        await SeedSource(($"CustomChecks/{Guid.NewGuid()}", new { CustomCheckId = "Sales.HealthCheck", Category = "Ping", Status = (int)Status.Pass, ReportedAt = DateTime.UtcNow }));

        var result = await CopyCategory("CustomChecks");
        var checkpoint = await ReadCheckpoint("CustomChecks");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Saved.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors), "one fault skip ends a required category Failed");
            Assert.That(checkpoint.CopiedCount, Is.Zero);
            Assert.That(checkpoint.SkipReasons?.GetValueOrDefault(MigrationSkipReason.RequiredValueMissing), Is.EqualTo(1), "CustomChecks.OriginatingEndpointName is NOT NULL, and a throw would stop the batch rather than count the row");
        }
    }

    [Test]
    public async Task Only_a_check_whose_id_and_category_match_a_RavenDB_persister_check_is_skipped_as_harmless_and_the_category_ends_done()
    {
        var serviceControl = new EndpointDetails { Name = "Particular.ServiceControl", Host = "MACHINE", HostId = Guid.NewGuid() };
        var audit = new EndpointDetails { Name = "Particular.ServiceControl.Audit", Host = "MACHINE", HostId = Guid.NewGuid() };
        await SeedSourceCustomCheck(serviceControl, "Error Database Index Lag", "ServiceControl Health", failed: true, reason: "index lag");
        await SeedSourceCustomCheck(audit, "Audit Database Index Lag", "ServiceControl.Audit Health", failed: true, reason: "index lag");
        await SeedSourceCustomCheck(audit, "RavenDB dirty memory", "ServiceControl.Audit Health", failed: true, reason: "dirty memory");
        await SeedSourceCustomCheck(Endpoint, "Sales.HealthCheck", "Ping", failed: true, reason: "timed out");
        await SeedSourceCustomCheck(Endpoint, "Message Ingestion Process", "Sales", failed: true, reason: "stuck");

        var result = await CopyCategory("CustomChecks");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Saved.State, Is.EqualTo(MigrationCategoryState.Complete), "a harmless skip leaves a required category Done, so ServiceControl can open");
            Assert.That(result.Saved.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.SourceStorageCheck] = 1 }), "only the primary's index lag check is the RavenDB persister's own");
            Assert.That((await CopiedChecks()).Select(check => (check.CustomCheckId, check.Category)), Is.EquivalentTo(new[]
            {
                ("Audit Database Index Lag", "ServiceControl.Audit Health"),
                ("RavenDB dirty memory", "ServiceControl.Audit Health"),
                ("Sales.HealthCheck", "Ping"),
                ("Message Ingestion Process", "Sales")
            }), "the skip matches the id and the category, so the audit instance's checks and a customer check with a colliding id keep their failing results");
        }
    }
}
