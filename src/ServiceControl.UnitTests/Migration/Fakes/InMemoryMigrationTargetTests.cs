#nullable enable
namespace ServiceControl.UnitTests.Migration.Fakes;

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class InMemoryMigrationTargetTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    static MigrationCheckpoint EmptyCheckpoint(string categoryId) =>
        new(categoryId, Selected: true, MigrationCategoryState.InProgress, Cursor: null, 0, 0, null, null, null, null, null, null, null);

    [Test]
    public async Task Writes_new_rows_and_persists_the_checkpoint_it_was_handed_exactly_as_given()
    {
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var category = MigrationCategoryRegistry.Find("EndpointSettings")!;
        var batch = new MigrationBatch([Row("a"), Row("b")], Cursor: "b");
        // Absolute post-batch totals, computed by the caller. The target does no arithmetic on them.
        var checkpointAfterBatch = EmptyCheckpoint(category.Id) with { Cursor = "b", CopiedCount = 2 };

        var result = await target.Write(category, batch, checkpointAfterBatch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(2));
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(target.WrittenRows(category.Id), Has.Count.EqualTo(2));
            Assert.That(await checkpointStore.Read(category.Id), Is.EqualTo(checkpointAfterBatch));
        }
    }

    [Test]
    public async Task The_persisted_checkpoint_is_not_adjusted_by_what_the_write_actually_did()
    {
        // The engine's totals already count every row as copied, so a target that corrected them from
        // its own result would double the counts.
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        target.SeedExistingKey("already-present");
        target.RejectKey("rejected", "Rejected");
        var batch = new MigrationBatch([Row("already-present"), Row("rejected"), Row("new-row")], Cursor: "new-row");
        var checkpointAfterBatch = EmptyCheckpoint(category.Id) with { Cursor = "new-row", CopiedCount = 3 };

        var result = await target.Write(category, batch, checkpointAfterBatch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.EqualTo(1));
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "rejected" }));
            Assert.That(result.SkipReasons, Is.EquivalentTo(new Dictionary<string, long> { ["Rejected"] = 1 }));
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
            Assert.That(target.WrittenRows(category.Id), Has.Count.EqualTo(1));
            Assert.That((await checkpointStore.Read(category.Id))!.CopiedCount, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Count_answers_for_one_category_rather_than_one_table()
    {
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var required = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var archive = MigrationCategoryRegistry.Find("ArchivedAndResolvedFailedMessages")!;
        await target.Write(required, new MigrationBatch([Row("a"), Row("b")], "b"), EmptyCheckpoint(required.Id));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await target.Count(required), Is.EqualTo(2));
            Assert.That(await target.Count(archive), Is.Zero);
        }
    }
}
