#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationEngineHaltTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    [Test]
    public async Task A_systemic_failure_halts_the_category_partway_through()
    {
        var category = MigrationCategoryRegistry.Find("ArchivedAndResolvedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        // Every 5th of 1,000 rows is rejected: a steady 20% spread evenly rather than clustered at the
        // start, past both the 5% proportion and the 100-row floor.
        var rows = Enumerable.Range(1, 1_000).Select(i => Row($"row-{i}")).ToArray();
        source.Seed(category.Id, rows);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        foreach (var i in Enumerable.Range(1, 1_000).Where(i => i % 5 == 0))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.BodyUnreadable);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(checkpoint.LastError, Does.Contain("Halted"));
            Assert.That(checkpoint.SettledAt, Is.Not.Null);
            // Stopped partway: the 1,000th row was never reached.
            Assert.That(target.WrittenRows(category.Id).Count, Is.LessThan(800));
        }
    }

    [Test]
    public async Task Rows_the_target_would_have_deleted_anyway_never_count_toward_the_halt_threshold()
    {
        var category = MigrationCategoryRegistry.Find("ArchivedAndResolvedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        // The same 20% that halts the category above, except these rows are past the target's retention
        // cutoff, so leaving them behind is the copy working rather than failing.
        source.Seed(category.Id, [.. Enumerable.Range(1, 1_000).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        foreach (var i in Enumerable.Range(1, 1_000).Where(i => i % 5 == 0))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.PastRetention, benign: true);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors));
            Assert.That((checkpoint.CopiedCount, checkpoint.SkippedCount), Is.EqualTo((800L, 200L)));
            Assert.That(target.WrittenRows(category.Id), Has.Count.EqualTo(800), "the last row was reached, so nothing halted partway");
        }
    }

    [TestCase(25, MigrationCategoryState.CompleteWithErrors, TestName = "A_mix_of_benign_and_fault_skips_runs_on_while_the_faults_stay_under_the_threshold")]
    [TestCase(10, MigrationCategoryState.Halted, TestName = "A_mix_of_benign_and_fault_skips_halts_once_the_faults_alone_pass_the_threshold")]
    public async Task A_batch_mixing_benign_and_fault_skips_is_judged_on_the_faults_alone(int everyNthIsAFault, MigrationCategoryState expected)
    {
        // A real archive copy loses rows both ways at once: retention takes some, unreadable bodies take
        // others. This is the only shape where the subtraction has to do arithmetic rather than pick a side.
        var category = MigrationCategoryRegistry.Find("ArchivedAndResolvedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 5_000).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        // A fifth of the category is past retention either way, which on its own is four times the threshold.
        foreach (var i in Enumerable.Range(1, 5_000).Where(i => i % 5 == 0))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.PastRetention, benign: true);
        }
        // The offset keeps the faults clear of the benign rows: 4% of the category in one case, 10% in the other.
        foreach (var i in Enumerable.Range(1, 5_000).Where(i => i % everyNthIsAFault == 3))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.BodyUnreadable);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        Assert.That(checkpoint.State, Is.EqualTo(expected));
    }

    [Test]
    public async Task Rows_already_present_keep_a_category_under_the_halt_threshold()
    {
        // Already-present rows are in the denominator because the run did handle them. Drop them from it
        // and this category's 4% fault rate reads as 12%, halting a copy that is merely being re-run.
        var category = MigrationCategoryRegistry.Find("ArchivedAndResolvedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 3_000).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        foreach (var i in Enumerable.Range(1, 2_000))
        {
            target.SeedExistingKey($"row-{i}");
        }
        // 120 faults spread through the whole category: past the 100-row floor, and 4% of 3,000.
        foreach (var i in Enumerable.Range(1, 3_000).Where(i => i % 25 == 0))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.BodyUnreadable);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors));
            Assert.That(checkpoint.SkippedCount, Is.EqualTo(120));
            Assert.That(checkpoint.AlreadyPresentCount, Is.EqualTo(1_920), "the 80 already-present rows that are also faults are refused before the collision check");
        }
    }

    [Test]
    public async Task Rows_already_present_in_the_target_never_count_toward_the_halt_threshold()
    {
        var category = MigrationCategoryRegistry.Find("ArchivedAndResolvedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 1_000).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        foreach (var i in Enumerable.Range(1, 1_000).Where(i => i % 5 == 0))
        {
            target.SeedExistingKey($"row-{i}");
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(800));
            Assert.That(checkpoint.AlreadyPresentCount, Is.EqualTo(200));
        }
    }

    [Test]
    public async Task A_restart_after_a_threshold_halt_counts_only_its_own_skips_and_keeps_the_earlier_ones()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 1_000).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var failingTarget = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        foreach (var i in Enumerable.Range(1, 1_000).Where(i => i % 5 == 0))
        {
            failingTarget.RejectKey($"row-{i}", MigrationSkipReason.BodyUnreadable);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var halted = await new MigrationEngine(source, failingTarget, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance).RunCategoryAsync(category);

        // The cause is fixed: the remaining rows now write cleanly.
        var fixedTarget = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        var finished = await new MigrationEngine(source, fixedTarget, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance).RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            // 120 skips in 600 rows is the first point past both the floor and 5%.
            Assert.That((halted.State, halted.CopiedCount, halted.SkippedCount), Is.EqualTo((MigrationCategoryState.Halted, 480L, 120L)));
            Assert.That(finished.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors), "the skips still on the row must not halt a run that skips nothing");
            Assert.That((finished.CopiedCount, finished.SkippedCount), Is.EqualTo((880L, 120L)), "copied, skipped at the end");
        }
    }

    [Test]
    public async Task A_category_smaller_than_the_floor_that_loses_every_row_halts_rather_than_completing()
    {
        // Ninety rows is under the hundred-row floor, so the threshold the engine checks after every batch
        // can never fire, however many rows are lost.
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 90).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 30 };
        foreach (var i in Enumerable.Range(1, 90))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.RequiredValueMissing);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);

        var checkpoint = await new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance).RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted), "a required category that copied nothing must not let the host open");
            Assert.That(checkpoint.CopiedCount, Is.Zero);
            Assert.That(checkpoint.LastError, Does.Contain("most of them"));
        }
    }

    [Test]
    public async Task A_category_that_lost_every_row_stays_halted_when_it_is_restarted_with_nothing_fixed()
    {
        // The halt tells the operator to restart, and the restart resumes from a cursor already at the end,
        // so the run that clears the halt is the one that reads nothing and can judge nothing.
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 90).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 30 };
        foreach (var i in Enumerable.Range(1, 90))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.RequiredValueMissing);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var halted = await engine.RunCategoryAsync(category);
        var restarted = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(halted.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(restarted.State, Is.EqualTo(MigrationCategoryState.Halted), "a restart that copied nothing reported the category finished, and the host would open on an empty table");
            Assert.That(restarted.CopiedCount, Is.Zero);
        }
    }

    // A transient failure on the last read halts a category that copied everything. The restart reads nothing,
    // so a rule that asks only whether this run read rows would hold it halted with nothing left to fix.
    [Test]
    public async Task A_category_that_copied_every_row_before_it_halted_completes_on_the_restart()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 30).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 30 };
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var copied = await engine.RunCategoryAsync(category);
        await checkpointStore.Upsert(copied with { State = MigrationCategoryState.Halted, LastError = "the source connection reset on the last read" });

        var restarted = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restarted.State, Is.EqualTo(MigrationCategoryState.Complete), "a category holding every one of its rows was left halted with nothing an operator could fix");
            Assert.That(restarted.CopiedCount, Is.EqualTo(30));
        }
    }

    // The halt lives on the row, and the run that clears it is the one that settles. A start killed in between
    // must not leave the row saying the category is fine.
    [Test]
    public async Task A_restart_killed_before_it_settles_does_not_let_the_next_one_report_the_category_finished()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 90).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 30 };
        foreach (var i in Enumerable.Range(1, 90))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.RequiredValueMissing);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var halted = await engine.RunCategoryAsync(category);
        // What a start killed after the InProgress save but before the settle leaves behind.
        await checkpointStore.Upsert(halted with { State = MigrationCategoryState.InProgress, LastError = null, SettledAt = null });

        var restarted = await engine.RunCategoryAsync(category);

        Assert.That(restarted.State, Is.EqualTo(MigrationCategoryState.Halted), "an interrupted restart erased the halt, so the next one reported an empty category finished and the host would open");
    }

    [Test]
    public async Task A_small_category_losing_rows_the_product_would_drop_anyway_still_completes()
    {
        // Benign skips are rows the target would have deleted anyway, so no number of them may halt a
        // category, and the most-of-the-run rule must not be the exception that brings that back.
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 90).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 30 };
        foreach (var i in Enumerable.Range(1, 90))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.EndpointNotKnown, benign: true);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);

        var checkpoint = await new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance).RunCategoryAsync(category);

        Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors));
    }

    // The restart after a halt reads nothing, so the whole row is judged at once rather than this run alone.
    // Sixty of these ninety rows are gone and none of them is a loss, so there is nothing to stay halted for.
    [Test]
    public async Task A_category_halted_after_losing_only_rows_the_product_would_drop_anyway_completes_on_the_restart()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 90).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 30 };
        foreach (var i in Enumerable.Range(1, 90).Where(i => i % 3 != 0))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.EndpointNotKnown, benign: true);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var copied = await engine.RunCategoryAsync(category);
        await checkpointStore.Upsert(copied with { State = MigrationCategoryState.Halted, LastError = "the source connection reset on the last read" });

        var restarted = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restarted.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors), "a category that lost nothing the product wanted was left halted for ever, and the host never opens");
            Assert.That((restarted.CopiedCount, restarted.SkippedCount), Is.EqualTo((30L, 60L)));
        }
    }
}
