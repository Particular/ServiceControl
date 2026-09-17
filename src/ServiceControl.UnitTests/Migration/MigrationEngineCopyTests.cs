#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationEngineCopyTests
{
    static MigrationRow Row(string id) => new(id, new { Name = id }, new Dictionary<string, object?>());

    [Test]
    public async Task Copies_every_row_and_finishes_Complete_when_nothing_was_skipped()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2 };
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(3));
            Assert.That(checkpoint.SkippedCount, Is.Zero);
            Assert.That(checkpoint.Cursor, Is.EqualTo("c"));
            Assert.That(checkpoint.StartedAt, Is.Not.Null);
            Assert.That(checkpoint.SettledAt, Is.Not.Null);
            Assert.That(target.WrittenRows(category.Id), Has.Count.EqualTo(3));
        }
    }

    [Test]
    public async Task A_category_with_no_rows_finishes_Complete_without_a_cursor()
    {
        // The ordinary state of several required categories on a small instance: nothing to copy is a
        // finished category, not a category that never ran.
        var category = MigrationCategoryRegistry.Find("MessageRedirects")!;
        var source = new InMemoryMigrationSource();
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That((checkpoint.CopiedCount, checkpoint.SkippedCount), Is.EqualTo((0L, 0L)));
            Assert.That(checkpoint.Cursor, Is.Null);
            Assert.That(checkpoint.SettledAt, Is.Not.Null);
        }
    }

    [Test]
    public async Task The_moment_a_category_settles_comes_from_the_injected_clock()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var settledAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var clock = new FakeTimeProvider(settledAt);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, clock, options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        Assert.That(checkpoint.SettledAt, Is.EqualTo(settledAt.UtcDateTime), "a wall-clock read here would drift from every other time the migration reports");
    }

    [Test]
    public async Task A_category_already_CompleteWithErrors_is_left_alone_on_a_second_run()
    {
        // Finished with a few skips is finished. Re-reading it would copy the whole category again and
        // count its skips a second time.
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var finishedWithSkips = new MigrationCheckpoint(category.Id, MigrationCategoryState.CompleteWithErrors, "b", 1, 1, 2,
            new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 1 }, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null);
        await checkpointStore.Upsert(finishedWithSkips);
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint, Is.EqualTo(finishedWithSkips with { Version = 1 }), "the row is read back untouched, at the version the seeding save left it");
            Assert.That(target.WrittenRows(category.Id), Is.Empty);
        }
    }

    [Test]
    public async Task A_category_already_Complete_is_left_alone_on_a_second_run()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var alreadyDone = new MigrationCheckpoint(category.Id, MigrationCategoryState.Complete, "a", 1, 0, 1, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null);
        await checkpointStore.Upsert(alreadyDone);
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint, Is.EqualTo(alreadyDone with { Version = 1 }), "the row is read back untouched, at the version the seeding save left it");
            Assert.That(target.WrittenRows(category.Id), Is.Empty);
        }
    }
}
