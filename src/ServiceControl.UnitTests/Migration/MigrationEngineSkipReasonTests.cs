#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
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
        // One reason exists, so this pins the total rather than the split between reasons.
        target.RejectKey("a", MigrationSkipReason.BodyUnreadable);
        target.RejectKey("b", MigrationSkipReason.BodyUnreadable);
        target.RejectKey("d", MigrationSkipReason.BodyUnreadable);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.SkippedCount, Is.EqualTo(3));
            Assert.That(checkpoint.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 3 }));
            Assert.That((await checkpointStore.Read(category.Id))!.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 3 }));
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

    sealed class OverCountedBenignTarget(IMigrationCheckpointStore checkpointStore) : IMigrationTarget
    {
        public int BatchSizeFor(MigrationCategory category) => 10;

        public async Task<MigrationWriteResult> Write(MigrationCategory category, MigrationBatch batch, MigrationCheckpoint checkpointToExtend, CancellationToken cancellationToken = default)
        {
            var reasons = new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.PastRetention] = batch.Rows.Count };
            var saved = await checkpointStore.Upsert(checkpointToExtend.Extend(0, batch.Rows.Count, 0, reasons), cancellationToken);
            return new MigrationWriteResult(saved, 0, batch.Rows.Count, [], 0, reasons, BenignSkipped: batch.Rows.Count + 1);
        }

        public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) => Task.FromResult(0L);
    }

    sealed class UnexplainedSkipTarget(IMigrationCheckpointStore checkpointStore) : IMigrationTarget
    {
        public int BatchSizeFor(MigrationCategory category) => 10;

        public async Task<MigrationWriteResult> Write(MigrationCategory category, MigrationBatch batch, MigrationCheckpoint checkpointToExtend, CancellationToken cancellationToken = default)
        {
            var saved = await checkpointStore.Upsert(checkpointToExtend.Extend(0, batch.Rows.Count, 0, null), cancellationToken);
            return new MigrationWriteResult(saved, 0, batch.Rows.Count, []);
        }

        public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) => Task.FromResult(0L);
    }
}
