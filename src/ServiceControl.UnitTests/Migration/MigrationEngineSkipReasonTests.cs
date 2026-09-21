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
class MigrationEngineSkipReasonTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    [Test]
    public async Task Reasons_the_target_reports_add_up_across_batches_on_the_checkpoint()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"), Row("d"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 3 };
        // Two reasons over two batches: a and b land in the first, d in the second, so the saved map has to merge both.
        target.RejectKey("a", MigrationSkipReason.RequiredValueMissing);
        target.RejectKey("b", MigrationSkipReason.RequiredValueMissing);
        target.RejectKey("d", MigrationSkipReason.EndpointNotKnown);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.SkippedCount, Is.EqualTo(3));
            Assert.That(checkpoint.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 2, [MigrationSkipReason.EndpointNotKnown] = 1 }));
            Assert.That((await checkpointStore.Read(category.Id))!.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 2, [MigrationSkipReason.EndpointNotKnown] = 1 }));
        }
    }

    [Test]
    public async Task A_message_whose_body_is_never_read_is_counted_under_BodyUnreadable()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1"), Row("msg-2"));
        source.FailBodyReads("msg-1", MigrationEngine.MaxBodyReadAttempts, new TimeoutException("down"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        Assert.That(checkpoint.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 1 }));
    }

    [Test]
    public async Task A_batch_that_loses_rows_two_different_ways_records_both_reasons()
    {
        // The breakdown is what tells a customer what they lost and why. A merge that overwrote instead of
        // summing would leave the total right and the reasons wrong, and verification would still balance.
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1"), Row("msg-2"), Row("msg-3"));
        // One the engine skips itself because the body will not read, one the target refuses.
        source.FailBodyReads("msg-1", MigrationEngine.MaxBodyReadAttempts, new TimeoutException("body store unreachable"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 3 };
        target.RejectKey("msg-2", MigrationSkipReason.PastRetention, benign: true);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors));
            Assert.That(checkpoint.SkippedCount, Is.EqualTo(2));
            Assert.That(checkpoint.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.BodyUnreadable] = 1,
                [MigrationSkipReason.PastRetention] = 1
            }));
            Assert.That(target.WrittenRows(category.Id).Select(row => row.SourceId), Is.EqualTo(new[] { "msg-3" }));
        }
    }

    [Test]
    public async Task A_target_whose_skip_reasons_do_not_add_up_to_its_skips_halts_the_category()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new UnexplainedSkipTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(checkpoint.LastError, Does.Contain("reported 1 skipped").And.Contain("reasons for 0"));
            Assert.That(checkpoint.Cursor, Is.Null, "the target refused ahead of its commit, so nothing was written and a restart reads the batch again");
        }
    }

    [Test]
    public async Task A_target_counting_more_benign_skips_than_skipped_rows_halts_the_category()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new OverCountedBenignTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(checkpoint.LastError, Does.Contain("2 benign skips").And.Contain("out of 1 skipped"));
        }
    }

    [Test]
    public async Task A_target_whose_reported_counts_disagree_with_the_checkpoint_it_committed_never_finishes_the_category()
    {
        var category = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, [.. Enumerable.Range(1, 300).Select(i => Row($"row-{i}"))]);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new MiscountedCopyTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var settled = await engine.RunCategoryAsync(category);

        var stored = await checkpointStore.Read(category.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settled.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(stored!.LastError, Does.Contain("but the checkpoint it committed moved by"), "the halt has to name which two accounts disagreed, or the operator goes looking for the wrong problem");
            Assert.That(stored.State.IsFinished(), Is.False, "the halt threshold never saw the 150 rows the target dropped, so the category settled finished and the host opened on half a category");
        }
    }

    sealed class OverCountedBenignTarget(IMigrationCheckpointStore checkpointStore) : IMigrationTarget
    {
        public Task Open(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public int BatchSizeFor(MigrationCategory category) => 10;

        public async Task<MigrationWriteResult> Write(MigrationCategory category, MigrationBatch batch, MigrationCheckpoint checkpointToExtend, CancellationToken cancellationToken = default)
        {
            var reasons = new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.PastRetention] = batch.Rows.Count };
            var saved = await checkpointStore.Upsert(checkpointToExtend.Extend(0, batch.Rows.Count, 0, reasons), cancellationToken);
            return new MigrationWriteResult(saved, 0, batch.Rows.Count, [], 0, reasons, BenignSkipped: batch.Rows.Count + 1);
        }

        public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) => Task.FromResult(0L);

        public IReadOnlyCollection<string> SupportedCategoryIds => [.. MigrationCategoryRegistry.All.Select(category => category.Id)];
    }

    // Commits half of every batch as skipped and reports the whole batch copied. The saved counts still add up
    // to the source total, so nothing later in the run can notice, and the halt threshold sees a clean copy.
    sealed class MiscountedCopyTarget(IMigrationCheckpointStore checkpointStore) : IMigrationTarget
    {
        public Task Open(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public int BatchSizeFor(MigrationCategory category) => 10;

        public async Task<MigrationWriteResult> Write(MigrationCategory category, MigrationBatch batch, MigrationCheckpoint checkpointToExtend, CancellationToken cancellationToken = default)
        {
            var skipped = batch.Rows.Count / 2;
            var reasons = new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = skipped };
            var saved = await checkpointStore.Upsert(checkpointToExtend.Extend(batch.Rows.Count - skipped, skipped, 0, reasons), cancellationToken);
            return new MigrationWriteResult(saved, batch.Rows.Count, 0, []);
        }

        public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) => Task.FromResult(0L);

        public IReadOnlyCollection<string> SupportedCategoryIds => [.. MigrationCategoryRegistry.All.Select(category => category.Id)];
    }

    sealed class UnexplainedSkipTarget(IMigrationCheckpointStore checkpointStore) : IMigrationTarget
    {
        public Task Open(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public int BatchSizeFor(MigrationCategory category) => 10;

        public async Task<MigrationWriteResult> Write(MigrationCategory category, MigrationBatch batch, MigrationCheckpoint checkpointToExtend, CancellationToken cancellationToken = default)
        {
            var saved = await checkpointStore.Upsert(checkpointToExtend.Extend(0, batch.Rows.Count, 0, null), cancellationToken);
            return new MigrationWriteResult(saved, 0, batch.Rows.Count, []);
        }

        public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) => Task.FromResult(0L);

        public IReadOnlyCollection<string> SupportedCategoryIds => [.. MigrationCategoryRegistry.All.Select(category => category.Id)];
    }
}
