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
    public async Task A_first_run_leaves_the_source_total_null_and_every_batch_moves_LastProgressAt()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"), Row("d"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2 };
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var betweenBatches = TimeSpan.FromMinutes(1);
        var clock = new FakeTimeProvider(startedAt);
        var stamps = new List<DateTime?>();
        // The engine stamps the checkpoint before the target sees it, so moving the clock in here is what
        // makes a stamp that never moves show up as two identical entries.
        target.BeforeWrite = extending =>
        {
            stamps.Add(extending.LastProgressAt);
            clock.Advance(betweenBatches);
        };
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, clock, options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.SourceTotal, Is.Null, "counting the source is a full pass over it, which on a big category could trip the stall watchdog before a row is copied");
            Assert.That(stamps, Is.EqualTo(new DateTime?[] { startedAt.UtcDateTime, (startedAt + betweenBatches).UtcDateTime }), "the stall watchdog stops a copy whose LastProgressAt stops moving, so every batch has to move it");
            Assert.That(checkpoint.LastProgressAt, Is.EqualTo((startedAt + betweenBatches).UtcDateTime), "the saved row carries the last batch's stamp");
        }
    }

    [Test]
    public async Task A_category_that_read_fewer_rows_than_the_source_holds_still_settles_complete()
    {
        // The row says the source held 10 and the source yields 2. The engine judges only its own accounting,
        // so a read that ends early is not something it can see, and only --migration-verify shows it.
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("d"), Row("e"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        await checkpointStore.Upsert(new MigrationCheckpoint(category.Id, MigrationCategoryState.InProgress, null, 0, 0, 10, null, DateTime.UtcNow, DateTime.UtcNow, null, null));
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(2));
            Assert.That(checkpoint.SettledAt, Is.Not.Null);
        }
    }

    [Test]
    public async Task An_optional_category_whose_outcomes_do_not_add_up_to_the_rows_read_settles_halted()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.EventLog)!;
        var checkpoint = await RunWithOneRowUnaccountedFor(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(category.Kind, Is.EqualTo(MigrationCategoryKind.Optional));
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(checkpoint.LastError, Does.Contain("read 4 rows").And.Contain("accounted for 3"));
            Assert.That(checkpoint.LastError, Does.Not.Contain("--migration-retry").And.Not.Contain("--migration-abandon"));
        }
    }

    [Test]
    public async Task A_required_category_whose_outcomes_do_not_add_up_to_the_rows_read_settles_halted()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var checkpoint = await RunWithOneRowUnaccountedFor(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(category.Kind, Is.EqualTo(MigrationCategoryKind.Required));
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(checkpoint.LastError, Does.Contain("read 4 rows").And.Contain("accounted for 3"));
            Assert.That(checkpoint.LastError, Does.Not.Contain("--migration-retry").And.Not.Contain("--migration-abandon"));
        }
    }

    // The target commits a checkpoint and a result that agree with each other, so only the rows read can show the one it lost.
    static Task<MigrationCheckpoint> RunWithOneRowUnaccountedFor(MigrationCategory category)
    {
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"), Row("d"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 4, UnderReportBy = 1 };
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        return engine.RunCategoryAsync(category);
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
        // Failed waits for the operator, so a start returns the row as it is rather than reading the category again.
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
    public async Task A_category_already_Halted_is_left_alone_on_a_second_run()
    {
        // Failed waits for the operator, so a start returns the row as it is rather than reading the category again.
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var halted = new MigrationCheckpoint(category.Id, MigrationCategoryState.Halted, "a", 1, 0, null, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, "Halted: the target was unreachable");
        await checkpointStore.Upsert(halted);
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint, Is.EqualTo(halted with { Version = 1 }), "the row is read back untouched, at the version the seeding save left it");
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
