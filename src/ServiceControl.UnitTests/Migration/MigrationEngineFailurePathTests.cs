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

        target.FailOnCallNumber = null;
        var secondRun = BuildEngine(source, checkpointStore, target);
        var finished = await secondRun.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finished.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(finished.LastError, Is.Null, "a cleared halt does not leave a stale error on the row");
            Assert.That(finished.CopiedCount, Is.EqualTo(4));
            var writtenIds = target.WrittenRows(category.Id).Select(r => r.SourceId).ToArray();
            Assert.That(writtenIds, Is.EquivalentTo(new[] { "a", "b", "c", "d" }));
            Assert.That(writtenIds.Distinct().Count(), Is.EqualTo(writtenIds.Length), "no duplicates across the halt");
        }
    }

    [Test]
    public async Task Body_skips_in_a_batch_whose_write_fails_are_counted_once_across_the_restart()
    {
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1"), Row("msg-2"));
        // msg-1's body is unreadable on both runs: every attempt fails on each.
        for (var attempt = 0; attempt < 2 * MigrationEngine.MaxBodyReadAttempts; attempt++)
        {
            source.QueueBodyAttempt("msg-1", () => throw new InvalidOperationException("body store unreachable"));
        }
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
        var abandoned = new MigrationCheckpoint(category.Id, true, MigrationCategoryState.Abandoned, "a", 1, 3, 4, null, DateTime.UtcNow, DateTime.UtcNow, null, DateTime.UtcNow, "Halted: the body store was unreachable");
        await checkpointStore.Upsert(abandoned);
        var target = new InMemoryMigrationTarget(checkpointStore);
        var engine = BuildEngine(source, checkpointStore, target);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint, Is.EqualTo(abandoned));
            Assert.That(target.WrittenRows(category.Id), Is.Empty);
        }
    }
}
