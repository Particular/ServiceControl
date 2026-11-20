namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

class CustomChecksMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory Category = MigrationCategoryRegistry.Find(MigrationCategoryIds.CustomChecks)!;

    static readonly EndpointDetails Endpoint = new() { Name = "Sales", Host = "MACHINE", HostId = Guid.NewGuid() };

    [Test]
    public async Task A_check_the_target_already_holds_is_counted_as_already_present_and_keeps_its_stored_status()
    {
        var first = await Target.Write(Category, BatchOf(("CustomChecks/1", Detail(failed: false))), CheckpointAfter("CustomChecks/1"));
        var second = await Target.Write(Category, BatchOf(("CustomChecks/1", Detail(failed: true))), first.Saved);

        var stored = (await CustomChecks.GetStats(new PagingInfo())).Results!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Copied, Is.Zero);
            Assert.That(second.AlreadyPresent, Is.EqualTo(1), "a replayed batch finds its row already there, where a plain insert would fail the batch on the duplicate key");
            Assert.That(stored.Single().Status, Is.EqualTo(Status.Pass), "the copy never replaces a row the target already holds");
            Assert.That(await Target.Count(Category), Is.EqualTo(1));
        }
    }

    [TestCase(nameof(CustomCheckEntity.CustomCheckId))]
    [TestCase(nameof(CustomCheckEntity.Category))]
    [TestCase(nameof(CustomCheckEntity.OriginatingEndpointHost))]
    public async Task A_check_with_no_value_for_a_required_column_is_skipped_as_a_required_value_missing_and_the_rest_copy(string column)
    {
        var missing = column switch
        {
            nameof(CustomCheckEntity.CustomCheckId) => Detail(failed: true, checkId: null),
            nameof(CustomCheckEntity.Category) => Detail(failed: true, category: null),
            _ => Detail(failed: true, endpoint: new EndpointDetails { Name = "Sales", Host = null, HostId = Guid.NewGuid() })
        };
        var batch = BatchOf(("CustomChecks/1", missing), ("CustomChecks/2", Detail(failed: false, checkId: "Sales.DiskCheck")));

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new CustomChecksWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(Category, batch, CheckpointAfter("CustomChecks/2"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1), "a skip copies the rest of the batch, where a NOT NULL failure would fail the whole insert");
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 1 }));
            Assert.That(details, Is.EqualTo(new[] { $"CustomChecks.{column} is NOT NULL and the document has no value for it" }));
        }
    }

    [Test]
    public async Task A_row_the_source_flags_as_a_storage_check_is_skipped_as_harmless_whatever_its_id()
    {
        var batch = new MigrationBatch(
        [
            new MigrationRow("CustomChecks/1", Detail(failed: false, checkId: "Some.Other.Source.Check"), Flag(true)),
            new MigrationRow("CustomChecks/2", Detail(failed: false, checkId: "Sales.DiskCheck"), new Dictionary<string, object>())
        ], "CustomChecks/2");

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new CustomChecksWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(Category, batch, CheckpointAfter("CustomChecks/2"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.SourceStorageCheck] = 1 }));
            Assert.That(result.SkipReasons.Keys.All(reason => reason.IsBenign()), Is.True);
            Assert.That(details, Is.EqualTo(new[] { "'Some.Other.Source.Check' under 'Ping' is a check the source's own storage runs about itself, which nothing reports once the instance runs on the target" }));
        }
    }

    [Test]
    public async Task A_row_with_a_RavenDB_persister_id_and_category_but_no_flag_is_copied()
    {
        var result = await Target.Write(Category, BatchOf(("CustomChecks/1", Detail(failed: false, checkId: "Error Database Index Lag", category: "ServiceControl Health"))), CheckpointAfter("CustomChecks/1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1), "the target holds no list of ids, so only the source's flag can skip a row");
            Assert.That(result.SkipReasons, Is.Null);
        }
    }

    [Test]
    public void A_flag_that_is_not_a_bool_fails_the_batch_naming_the_row()
    {
        var batch = new MigrationBatch([new MigrationRow("CustomChecks/1", Detail(failed: false), Flag("true"))], "CustomChecks/1");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Target.Write(Category, batch, CheckpointAfter("CustomChecks/1")));

        Assert.That(exception.Message, Does.Contain("'CustomChecks/1'").And.Contain("System.String"));
    }

    [Test]
    public async Task A_row_whose_storage_check_flag_is_false_is_copied()
    {
        var batch = new MigrationBatch([new MigrationRow("CustomChecks/1", Detail(failed: false), Flag(false))], "CustomChecks/1");

        var result = await Target.Write(Category, batch, CheckpointAfter("CustomChecks/1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkipReasons, Is.Null);
        }
    }

    static Dictionary<string, object> Flag(object value) => new() { [MigrationMetadataKeys.SourceStorageCheck] = value };

    static CustomCheckDetail Detail(bool failed, string checkId = "Sales.HealthCheck", string category = "Ping", EndpointDetails endpoint = null) => new()
    {
        OriginatingEndpoint = endpoint ?? Endpoint,
        CustomCheckId = checkId,
        Category = category,
        ReportedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        HasFailed = failed
    };

    static MigrationBatch BatchOf(params (string Id, CustomCheckDetail Document)[] rows) =>
        new([.. rows.Select(row => new MigrationRow(row.Id, row.Document, new Dictionary<string, object>()))], rows[^1].Id);

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.CustomChecks, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);
}
