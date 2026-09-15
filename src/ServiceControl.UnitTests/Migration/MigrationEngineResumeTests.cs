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
}
