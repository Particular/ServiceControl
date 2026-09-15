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
        target.RejectKey("a", "KeyTooLong");
        target.RejectKey("b", "Unparseable");
        target.RejectKey("d", "KeyTooLong");
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.SkippedCount, Is.EqualTo(3));
            Assert.That(checkpoint.SkipReasons, Is.EquivalentTo(new Dictionary<string, long> { ["KeyTooLong"] = 2, ["Unparseable"] = 1 }));
            Assert.That((await checkpointStore.Read(category.Id))!.SkipReasons, Is.EquivalentTo(new Dictionary<string, long> { ["KeyTooLong"] = 2, ["Unparseable"] = 1 }));
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

        Assert.That(checkpoint.SkipReasons, Is.EquivalentTo(new Dictionary<string, long> { [nameof(MigrationSkipReason.BodyUnreadable)] = 1 }));
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
            Assert.That(checkpoint.Cursor, Is.EqualTo("a"), "the target committed the batch, so a restart must carry on after it");
        }
    }

    sealed class UnexplainedSkipTarget(IMigrationCheckpointStore checkpointStore) : IMigrationTarget
    {
        public int BatchSizeFor(MigrationCategory category) => 10;

        public async Task<MigrationWriteResult> Write(MigrationCategory category, MigrationBatch batch, MigrationCheckpoint checkpointAfterBatch, CancellationToken cancellationToken = default)
        {
            await checkpointStore.Upsert(checkpointAfterBatch, cancellationToken);
            return new MigrationWriteResult(0, batch.Rows.Count, []);
        }

        public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) => Task.FromResult(0L);
    }
}
