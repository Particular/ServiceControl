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
        new(categoryId, MigrationCategoryState.InProgress, Cursor: null, 0, 0, null, null, null, null, null, null);

    [Test]
    public async Task Writes_new_rows_and_commits_the_extended_checkpoint_with_them()
    {
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var category = MigrationCategoryRegistry.Find("EndpointSettings")!;
        var batch = new MigrationBatch([Row("a"), Row("b")], Cursor: "b");
        // Prior totals and the new cursor. The target adds this batch's own outcome before it saves.
        var checkpointToExtend = EmptyCheckpoint(category.Id) with { Cursor = "b" };

        var result = await target.Write(category, batch, checkpointToExtend);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(2));
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(target.WrittenRows(category.Id), Has.Count.EqualTo(2));
            Assert.That(await checkpointStore.Read(category.Id), Is.EqualTo(checkpointToExtend with { CopiedCount = 2, Version = 1 }));
            Assert.That(result.Saved, Is.EqualTo(checkpointToExtend with { CopiedCount = 2, Version = 1 }), "the result carries the row as stored");
        }
    }

    [Test]
    public async Task The_committed_checkpoint_carries_the_real_split_rather_than_the_rows_handed_over()
    {
        // Three rows in, one copied: a checkpoint saying three would survive a crash as three.
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        target.SeedExistingKey("already-present");
        target.RejectKey("rejected", MigrationSkipReason.BodyUnreadable);
        var batch = new MigrationBatch([Row("already-present"), Row("rejected"), Row("new-row")], Cursor: "new-row");
        var checkpointToExtend = EmptyCheckpoint(category.Id) with { Cursor = "new-row" };

        var result = await target.Write(category, batch, checkpointToExtend);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.EqualTo(1));
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "rejected" }));
            Assert.That(result.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 1 }));
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
            Assert.That(target.WrittenRows(category.Id), Has.Count.EqualTo(1));
            var stored = (await checkpointStore.Read(category.Id))!;
            Assert.That((stored.CopiedCount, stored.SkippedCount, stored.AlreadyPresentCount), Is.EqualTo((1L, 1L, 1L)), "copied, skipped, already present as committed");
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
