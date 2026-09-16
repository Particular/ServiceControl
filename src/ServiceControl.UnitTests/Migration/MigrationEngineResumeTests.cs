#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationEngineResumeTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    [Test]
    public async Task Restarting_after_a_mid_category_stop_produces_no_duplicates_and_no_gaps()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        var allIds = Enumerable.Range(1, 6).Select(i => $"row-{i}").ToArray();
        source.Seed(category.Id, [.. allIds.Select(Row)]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        using var stopping = new CancellationTokenSource();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2, StopOnCall = (3, stopping) };
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var firstEngine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        // Batches are 2 rows each; the host stops on the 3rd call to Write, so exactly 2 batches (4 rows) land.
        Assert.ThrowsAsync<OperationCanceledException>(() => firstEngine.RunCategoryAsync(category, stopping.Token));

        var afterStop = await checkpointStore.Read(category.Id);
        using (Assert.EnterMultipleScope())
        {
            // Still InProgress, not Halted: a cancelled run is a shutdown, not a failure.
            Assert.That(afterStop!.State, Is.EqualTo(MigrationCategoryState.InProgress));
            Assert.That(afterStop.CopiedCount, Is.EqualTo(4));
            Assert.That(afterStop.Cursor, Is.EqualTo("row-4"));
            Assert.That(target.WrittenRows(category.Id), Has.Count.EqualTo(4));
        }

        // Restart: same source, same target, same checkpoint store, nothing stopping it this time.
        target.StopOnCall = null;
        var secondEngine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);
        var finalCheckpoint = await secondEngine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finalCheckpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(finalCheckpoint.CopiedCount, Is.EqualTo(6));
            var writtenIds = target.WrittenRows(category.Id).Select(r => r.SourceId).ToArray();
            Assert.That(writtenIds, Is.EquivalentTo(allIds), "no gaps");
            Assert.That(writtenIds.Distinct().Count(), Is.EqualTo(writtenIds.Length), "no duplicates");
        }
    }

    [Test]
    public async Task A_graceful_stop_saves_the_real_split_of_the_last_committed_batch()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 6).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        using var stopping = new CancellationTokenSource();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2, StopOnCall = (3, stopping) };
        // Both in the second batch, the last one to commit before the stop.
        target.SeedExistingKey("row-3");
        target.RejectKey("row-4", MigrationSkipReason.BodyUnreadable);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var firstEngine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        Assert.ThrowsAsync<OperationCanceledException>(() => firstEngine.RunCategoryAsync(category, stopping.Token));
        var afterStop = await checkpointStore.Read(category.Id);

        target.StopOnCall = null;
        var secondEngine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);
        var finalCheckpoint = await secondEngine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterStop!.State, Is.EqualTo(MigrationCategoryState.InProgress));
            Assert.That((afterStop.CopiedCount, afterStop.SkippedCount, afterStop.AlreadyPresentCount), Is.EqualTo((2L, 1L, 1L)), "copied, skipped, already present after the stop");
            Assert.That(finalCheckpoint.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors));
            Assert.That((finalCheckpoint.CopiedCount, finalCheckpoint.SkippedCount, finalCheckpoint.AlreadyPresentCount), Is.EqualTo((4L, 1L, 1L)), "copied, skipped, already present at the end");
            Assert.That(finalCheckpoint.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 1 }));
        }
    }

    [Test]
    public async Task A_hard_crash_after_a_committed_write_loses_nothing_because_the_target_saved_the_real_split()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 4).Select(i => Row($"row-{i}"))]);
        var committed = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(committed) { DefaultBatchSize = 2 };
        target.SeedExistingKey("row-3");
        target.RejectKey("row-4", MigrationSkipReason.BodyUnreadable);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var crashingEngine = new MigrationEngine(source, target, new CrashAfterCommitCheckpointStore(committed, crashAfterCursor: "row-4"), new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);
        await crashingEngine.RunCategoryAsync(category);

        var restartedEngine = new MigrationEngine(source, target, committed, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);
        var finalCheckpoint = await restartedEngine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            // The engine's own settle was lost, but every count came from the target's own transaction.
            Assert.That(finalCheckpoint.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors));
            Assert.That((finalCheckpoint.CopiedCount, finalCheckpoint.SkippedCount, finalCheckpoint.AlreadyPresentCount), Is.EqualTo((2L, 1L, 1L)), "copied, skipped, already present at the end");
            Assert.That(target.WrittenRows(category.Id).Select(r => r.SourceId), Is.EqualTo(new[] { "row-1", "row-2" }));
        }
    }

    // Once the target has committed crashAfterCursor, the engine's own saves are lost, as if the process died there.
    sealed class CrashAfterCommitCheckpointStore(IMigrationCheckpointStore committed, string crashAfterCursor) : IMigrationCheckpointStore
    {
        public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default) => committed.ReadAll(cancellationToken);

        public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) => committed.Read(categoryId, cancellationToken);

        public async Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default) =>
            (await committed.Read(checkpoint.CategoryId, cancellationToken))?.Cursor == crashAfterCursor
                ? checkpoint
                : await committed.Upsert(checkpoint, cancellationToken);
    }
}
