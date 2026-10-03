#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationEngineFailurePathTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    static MigrationEngine BuildEngine(InMemoryMigrationSource source, InMemoryMigrationCheckpointStore checkpointStore, InMemoryMigrationTarget target) =>
        new(source, target, checkpointStore, new FakeTimeProvider(), new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), NullLogger<MigrationEngine>.Instance);

    [Test]
    public async Task A_failing_write_halts_the_category_and_records_the_error_instead_of_throwing()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"), Row("d"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2, FailOnCallNumber = 2 };
        var engine = BuildEngine(source, checkpointStore, target);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(checkpoint.LastError, Does.Contain("Simulated failure"));
            // The first batch committed, so the cursor is real and a later run resumes from it.
            Assert.That(checkpoint.Cursor, Is.EqualTo("b"));
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task The_halt_is_durable_so_the_health_check_and_the_guard_can_read_it()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2, FailOnCallNumber = 1 };
        var engine = BuildEngine(source, checkpointStore, target);

        await engine.RunCategoryAsync(category);

        var persisted = await checkpointStore.Read(category.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(persisted!.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(persisted.LastError, Is.Not.Null);
        }
    }

    [Test]
    public void A_cancelled_run_is_a_shutdown_rather_than_a_failure_and_is_not_swallowed()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        using var stopping = new CancellationTokenSource();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2, StopOnCall = (1, stopping) };
        var engine = BuildEngine(source, checkpointStore, target);

        Assert.ThrowsAsync<OperationCanceledException>(() => engine.RunCategoryAsync(category, stopping.Token));
    }

    [Test]
    public async Task A_halted_category_is_re_attempted_on_the_next_run_and_resumes_from_its_cursor()
    {
        // A halt that no restart can clear would leave abandoning the category as the only way out of
        // a fault the customer has already repaired.
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"), Row("d"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2, FailOnCallNumber = 2 };
        var firstRun = BuildEngine(source, checkpointStore, target);
        var halted = await firstRun.RunCategoryAsync(category);
        Assert.That(halted.State, Is.EqualTo(MigrationCategoryState.Halted));

        // Call 3 is the restarted run's first write, so the row read back next is a resumed copy, not a finished one.
        target.FailOnCallNumber = null;
        using var stopping = new CancellationTokenSource();
        target.StopOnCall = (3, stopping);
        Assert.ThrowsAsync<OperationCanceledException>(() => BuildEngine(source, checkpointStore, target).RunCategoryAsync(category, stopping.Token));
        var restarted = await checkpointStore.Read(category.Id);

        target.StopOnCall = null;
        var lastRun = BuildEngine(source, checkpointStore, target);
        var finished = await lastRun.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restarted!.State, Is.EqualTo(MigrationCategoryState.InProgress));
            Assert.That(restarted.SettledAt, Is.Null, "a copy running again does not keep the time its halt settled at");
            Assert.That(finished.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(finished.LastError, Is.Null, "a cleared halt does not leave a stale error on the row");
            Assert.That(finished.CopiedCount, Is.EqualTo(4));
            Assert.That(target.WrittenRows(category.Id).Select(r => r.SourceId), Is.EquivalentTo(new[] { "a", "b", "c", "d" }));
            // The target de-duplicates, as the real ones do, so what it kept can never show a row sent twice.
            Assert.That(target.RowsHandedToWrite(category.Id).Select(r => r.SourceId), Is.Unique, "no duplicates across the halt");
        }
    }

    [Test]
    public async Task Body_skips_in_a_batch_whose_write_fails_are_counted_once_across_the_restart()
    {
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1"), Row("msg-2"));
        // msg-1's body is unreadable on both runs: every attempt fails on each.
        source.FailBodyReads("msg-1", 2 * MigrationEngine.MaxBodyReadAttempts, new TimeoutException("body store unreachable"));
        source.SetBody("msg-2", new MigrationBody(new byte[] { 2 }, "text/plain"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2, FailOnCallNumber = 1 };
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var firstRun = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);
        var halted = await firstRun.RunCategoryAsync(category);

        target.FailOnCallNumber = null;
        var secondRun = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);
        var finished = await secondRun.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(halted.State, Is.EqualTo(MigrationCategoryState.Halted));
            // The failed batch never committed, so its skip is recorded only when the batch is read again.
            Assert.That(halted.SkippedCount, Is.Zero);
            Assert.That(finished.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors));
            Assert.That(finished.SkippedCount, Is.EqualTo(1));
            Assert.That(target.WrittenRows(category.Id).Select(r => r.SourceId), Is.EqualTo(new[] { "msg-2" }));
        }
    }

    [Test]
    public async Task An_abandoned_category_is_left_exactly_as_it_is()
    {
        // Abandoned is the one end a person chooses: this category will never be copied, and the
        // guard and the health check both treat it as settled. A later run must not quietly restart it.
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var abandoned = new MigrationCheckpoint(category.Id, MigrationCategoryState.Abandoned, "a", 1, 3, 4, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, "Halted: the body store was unreachable");
        await checkpointStore.Upsert(abandoned);
        var target = new InMemoryMigrationTarget(checkpointStore);
        var engine = BuildEngine(source, checkpointStore, target);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint, Is.EqualTo(abandoned with { Version = 1 }), "the row is read back untouched, at the version the seeding save left it");
            Assert.That(target.WrittenRows(category.Id), Is.Empty);
        }
    }

    [Test]
    public async Task A_target_with_no_batch_size_for_a_category_halts_it_and_the_next_category_still_runs()
    {
        var source = new InMemoryMigrationSource();
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var unmapped = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var next = MigrationCategoryRegistry.Find("MessageRedirects")!;
        source.Seed(unmapped.Id, Row("k-1"));
        source.Seed(next.Id, Row("r-1"));
        var target = new InMemoryMigrationTarget(checkpointStore) { NoBatchSizeFor = unmapped.Id };
        var engine = BuildEngine(source, checkpointStore, target);

        var results = await engine.RunCategories([unmapped, next]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results[0].State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(results[0].LastError, Does.Contain("No batch size"));
            Assert.That(results[1].State, Is.EqualTo(MigrationCategoryState.Complete));
        }
    }

    [Test]
    public void A_write_failure_halt_logs_its_exception_before_it_settles()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"));
        var checkpointStore = new HaltSaveFailsCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { FailOnCallNumber = 1 };
        var logger = new CapturingLogger();
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), logger);

        Assert.ThrowsAsync<TimeoutException>(() => engine.RunCategoryAsync(category));

        Assert.That(logger.Entries.Where(e => e.Level == LogLevel.Error).Select(e => e.Exception?.Message), Does.Contain("Simulated failure on write 1"));
    }

    [Test]
    public void A_threshold_halt_logs_its_reason_before_it_settles()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"));
        var checkpointStore = new HaltSaveFailsCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        target.RejectKey("a", MigrationSkipReason.BodyUnreadable);
        var logger = new CapturingLogger();
        // A floor of zero lets the one rejected row halt the category.
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 0, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, logger);

        Assert.ThrowsAsync<TimeoutException>(() => engine.RunCategoryAsync(category));

        Assert.That(logger.Entries.Where(e => e.Level == LogLevel.Error).Select(e => e.Message), Has.Some.Contains("Halted: 1 of 1 rows skipped"));
    }

    [Test]
    public async Task A_shortfall_halt_logs_its_reason_before_it_settles()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("d"), Row("e"));
        var checkpointStore = new HaltSaveFailsCheckpointStore();
        // The row is counted at 10 against a source holding 2, so the copy reaches the end short of its total.
        await checkpointStore.Upsert(new MigrationCheckpoint(category.Id, MigrationCategoryState.InProgress, null, 0, 0, 10, null, DateTime.UtcNow, DateTime.UtcNow, null, null));
        var target = new InMemoryMigrationTarget(checkpointStore);
        var logger = new CapturingLogger();
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), logger);

        Assert.ThrowsAsync<TimeoutException>(() => engine.RunCategoryAsync(category));

        Assert.That(logger.Entries.Where(e => e.Level == LogLevel.Error).Select(e => e.Message), Has.Some.Contains("accounted for 2 of the 10 rows"), "the save that records the reason is the one that failed, so a halt that settles before it logs leaves an operator a copy that stopped with nothing saying why");
    }

    [Test]
    public async Task A_checkpoint_conflict_leaves_the_other_writer_alone_instead_of_halting_over_it()
    {
        // Two hosts pointed at one target is what the version token exists for. Settling this as halted
        // would write over the progress of whichever host is still copying.
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"));
        // Save 1 moves the row to in progress; save 2 is the first batch, by which point the other host has moved it on.
        var checkpointStore = new ConflictOnNthSaveCheckpointStore { ConflictOnSave = 2 };
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2 };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), NullLogger<MigrationEngine>.Instance);

        Assert.ThrowsAsync<MigrationCheckpointConflictException>(() => engine.RunCategoryAsync(category));

        var persisted = await checkpointStore.Read(category.Id);
        Assert.That(persisted!.State, Is.EqualTo(MigrationCategoryState.InProgress), "no halted row was written over the conflict");
    }

    [Test]
    public async Task A_cancellation_that_is_not_a_shutdown_halts_the_category_like_any_other_failure()
    {
        // An inner timeout surfaces as the same exception type as a host stopping, and only the token
        // says which. Treating a timeout as a shutdown would end the run with no reason on the row.
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore)
        {
            FailOnCallNumber = 1,
            FailWith = new OperationCanceledException("the query timed out")
        };
        var engine = BuildEngine(source, checkpointStore, target);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(checkpoint.LastError, Does.Contain("OperationCanceledException").And.Contain("the query timed out"));
        }
    }

    [Test]
    public async Task The_halt_reason_names_the_cursor_the_copy_had_reached()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"), Row("d"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2, FailOnCallNumber = 2 };
        var engine = BuildEngine(source, checkpointStore, target);

        var checkpoint = await engine.RunCategoryAsync(category);

        Assert.That(checkpoint.LastError, Does.Contain("at cursor b"), "the cursor is the only pointer an operator has to where it stopped");
    }

    [Test]
    public async Task The_halt_reason_says_at_the_start_when_the_first_batch_never_committed()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { FailOnCallNumber = 1 };
        var engine = BuildEngine(source, checkpointStore, target);

        var checkpoint = await engine.RunCategoryAsync(category);

        Assert.That(checkpoint.LastError, Does.Contain("at the start"));
    }

    [Test]
    public async Task Every_row_the_target_skips_is_named_in_the_log()
    {
        // The counts say how much was left behind. Only the log says which rows, and it is the way back
        // to them while the RavenDB database still exists.
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        target.RejectKey("b", MigrationSkipReason.PastRetention);
        var logger = new CapturingLogger();
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), logger);

        await engine.RunCategoryAsync(category);

        Assert.That(logger.Entries.Select(entry => entry.Message), Has.Some.EqualTo("Skipped b in category KnownEndpoints"));
    }

    [Test]
    public async Task A_stop_between_batches_leaves_the_row_in_progress_at_the_batch_that_committed()
    {
        // The stop lands in the source rather than in a write, so nothing is mid-transaction: the row
        // still has to describe the batches that did commit, and stay resumable.
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"), Row("d"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        using var stopping = new CancellationTokenSource();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2, CancelOnCall = (1, stopping) };
        var engine = BuildEngine(source, checkpointStore, target);

        Assert.ThrowsAsync<OperationCanceledException>(() => engine.RunCategoryAsync(category, stopping.Token));

        var persisted = await checkpointStore.Read(category.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(persisted!.State, Is.EqualTo(MigrationCategoryState.InProgress), "a shutdown is not a halt");
            Assert.That(persisted.Cursor, Is.EqualTo("b"));
            Assert.That(persisted.CopiedCount, Is.EqualTo(2));
            Assert.That(persisted.SettledAt, Is.Null);
        }
    }

    // Another host moved the row on between this host reading it and saving it.
    sealed class ConflictOnNthSaveCheckpointStore : IMigrationCheckpointStore
    {
        readonly InMemoryMigrationCheckpointStore saved = new();
        int saves;

        public int ConflictOnSave { get; init; }

        public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default) => saved.ReadAll(cancellationToken);

        public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) => saved.Read(categoryId, cancellationToken);

        public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default) =>
            ++saves == ConflictOnSave
                ? throw new MigrationCheckpointConflictException($"Checkpoint {checkpoint.CategoryId} was saved from version {checkpoint.Version}, but the stored row has moved on.")
                : saved.Upsert(checkpoint, cancellationToken);
    }

    // The store shares the target's database, which has become unreachable by the time the halt is saved.
    sealed class HaltSaveFailsCheckpointStore : IMigrationCheckpointStore
    {
        readonly InMemoryMigrationCheckpointStore saved = new();

        public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default) => saved.ReadAll(cancellationToken);

        public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) => saved.Read(categoryId, cancellationToken);

        public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default) =>
            checkpoint.State == MigrationCategoryState.Halted ? throw new TimeoutException("checkpoint store unreachable") : saved.Upsert(checkpoint, cancellationToken);
    }
}
