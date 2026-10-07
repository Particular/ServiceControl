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
            target.RejectKey($"row-{i}", MigrationSkipReason.PastRetention);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That((checkpoint.CopiedCount, checkpoint.SkippedCount), Is.EqualTo((800L, 200L)));
            Assert.That(target.WrittenRows(category.Id), Has.Count.EqualTo(800), "the last row was reached, so nothing halted partway");
        }
    }

    [TestCase(25, MigrationCategoryState.CompleteWithErrors, TestName = "A_mix_of_benign_and_fault_skips_runs_on_while_the_faults_stay_under_the_threshold")]
    [TestCase(10, MigrationCategoryState.Halted, TestName = "A_mix_of_benign_and_fault_skips_halts_once_the_faults_alone_pass_the_threshold")]
    public async Task A_batch_mixing_benign_and_fault_skips_is_judged_on_the_faults_alone(int everyNthIsAFault, MigrationCategoryState expected)
    {
        // A real archive copy loses rows both ways at once: retention takes some, unreadable bodies take
        // others. This is the only shape where the sum has to leave some reasons out.
        var category = MigrationCategoryRegistry.Find("ArchivedAndResolvedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 5_000).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        // A fifth of the category is past retention either way, which on its own is four times the threshold.
        foreach (var i in Enumerable.Range(1, 5_000).Where(i => i % 5 == 0))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.PastRetention);
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
    public async Task A_resumed_run_counts_only_its_own_skips_and_keeps_the_earlier_ones()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 1_000).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        // What a shutdown after the sixth batch leaves: 120 fault skips in 600 rows, past both the floor and 5% if counted again.
        await checkpointStore.Upsert(new MigrationCheckpoint(category.Id, MigrationCategoryState.InProgress, "row-600", 480, 120, null,
            new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 120 }, DateTime.UtcNow, DateTime.UtcNow, null, null));
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };

        var finished = await new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance).RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finished.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors), "the skips still on the row must not halt a run that skips nothing");
            Assert.That((finished.CopiedCount, finished.SkippedCount), Is.EqualTo((880L, 120L)), "copied, skipped at the end");
        }
    }

    [Test]
    public async Task A_category_smaller_than_the_floor_that_loses_every_row_ends_Failed_rather_than_Done()
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
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors));
            Assert.That(checkpoint.State.IsFinished(), Is.False, "a required category that copied nothing must not let the host open");
            Assert.That(checkpoint.CopiedCount, Is.Zero);
        }
    }

    [Test]
    public async Task A_halted_category_is_returned_untouched_and_its_source_is_not_read()
    {
        // Failed waits for the operator, so a start that read the category again would copy rows nobody asked for.
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 90).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var halted = new MigrationCheckpoint(category.Id, MigrationCategoryState.Halted, null, 0, 0, null, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, "Halted: the target was unreachable");
        await checkpointStore.Upsert(halted);
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 30 };
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint, Is.EqualTo(halted with { Version = 1 }), "the row is read back untouched, at the version the seeding save left it");
            Assert.That(target.RowsHandedToWrite(category.Id), Is.Empty);
        }
    }

    [Test]
    public async Task A_small_category_losing_rows_the_product_would_drop_anyway_still_completes()
    {
        // Harmless skips are rows the target would have deleted anyway, so no number of them may stop a
        // category or leave it Failed, however small the category is.
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 90).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 30 };
        foreach (var i in Enumerable.Range(1, 90))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.EndpointNotKnown);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);

        var checkpoint = await new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance).RunCategoryAsync(category);

        Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
    }

    [Test]
    public async Task A_run_that_ends_with_one_fault_skip_settles_CompleteWithErrors_however_small_the_share()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 1_000).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        target.RejectKey("row-500", MigrationSkipReason.RequiredValueMissing);
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);

        var checkpoint = await new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance).RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors), "one row in a thousand is far under the threshold, and it is still a row the product wanted");
            Assert.That((checkpoint.CopiedCount, checkpoint.SkippedCount), Is.EqualTo((999L, 1L)));
        }
    }

    [Test]
    public async Task A_run_whose_only_skips_are_harmless_settles_Complete_and_keeps_its_reasons()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"), Row("d"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 2 };
        target.RejectKey("a", MigrationSkipReason.PastRetention);
        target.RejectKey("b", MigrationSkipReason.EndpointNotKnown);
        target.RejectKey("c", MigrationSkipReason.BlankGroupComment);
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);

        var checkpoint = await new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance).RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That((checkpoint.CopiedCount, checkpoint.SkippedCount), Is.EqualTo((1L, 3L)), "a harmless skip is still a skip, and status and verify count it");
            Assert.That(checkpoint.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.PastRetention] = 1,
                [MigrationSkipReason.EndpointNotKnown] = 1,
                [MigrationSkipReason.BlankGroupComment] = 1
            }));
        }
    }

    [Test]
    public async Task The_threshold_halt_names_its_counts_and_no_command()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.ArchivedAndResolvedFailedMessages)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 1_000).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        foreach (var i in Enumerable.Range(1, 1_000).Where(i => i % 5 == 0))
        {
            target.RejectKey($"row-{i}", MigrationSkipReason.BodyUnreadable);
        }
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []);

        var checkpoint = await new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance).RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
            // 120 skips in 600 rows is the first point past both the floor and 5%.
            Assert.That(checkpoint.LastError, Does.Contain("120 of 600").And.Contain("5%").And.Contain("100 rows"));
            // The engine cannot know which commands a category may take, so the readers of the row add them.
            Assert.That(checkpoint.LastError, Does.Not.Contain("--migration-retry").And.Not.Contain("--migration-abandon").And.Not.Contain("restart to resume"));
        }
    }
}
