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
class MigrationEngineBodyRetryTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    [Test]
    public async Task A_body_that_fails_until_the_last_attempt_then_succeeds_is_written_with_its_body_attached()
    {
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1"));
        var body = new MigrationBody(new byte[] { 1 }, "text/plain");
        source.SetBody("msg-1", body);
        for (var attempt = 1; attempt < MigrationEngine.MaxBodyReadAttempts; attempt++)
        {
            source.QueueBodyAttempt("msg-1", () => throw new InvalidOperationException("blip"));
        }
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        // Zeroed because FakeTimeProvider is never advanced here: a 200 ms Task.Delay against a clock
        // nobody moves never completes, and the test hangs until the runner kills it.
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(checkpoint.SkippedCount, Is.Zero);
            Assert.That(target.WrittenRows(category.Id).Single().Body, Is.EqualTo(body));
        }
    }

    [Test]
    public async Task A_body_the_source_already_attached_is_written_without_reading_it_again()
    {
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        var attached = new MigrationBody(new byte[] { 7 }, "text/plain");
        source.Seed(category.Id, Row("msg-1") with { Body = attached });
        for (var attempt = 0; attempt < MigrationEngine.MaxBodyReadAttempts; attempt++)
        {
            source.QueueBodyAttempt("msg-1", () => throw new InvalidOperationException("a body read nobody needed"));
        }
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(target.WrittenRows(category.Id).Single().Body, Is.EqualTo(attached));
        }
    }

    [Test]
    public async Task Exhausted_attempts_skip_the_whole_message_and_count_toward_the_halt_threshold()
    {
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1"), Row("msg-2"));
        for (var attempt = 0; attempt < MigrationEngine.MaxBodyReadAttempts; attempt++)
        {
            source.QueueBodyAttempt("msg-1", () => throw new InvalidOperationException("down"));
        }
        source.SetBody("msg-2", new MigrationBody(new byte[] { 2 }, "text/plain"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            // msg-1 is excluded entirely: not written with a missing body, not written at all.
            Assert.That(target.WrittenRows(category.Id).Select(r => r.SourceId), Is.EqualTo(new[] { "msg-2" }));
            Assert.That(checkpoint.SkippedCount, Is.EqualTo(1));
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors));
        }
    }

    [Test]
    public async Task A_body_store_outage_across_many_messages_halts_the_category()
    {
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        var ids = Enumerable.Range(1, 1_000).Select(i => $"msg-{i}").ToArray();
        source.Seed(category.Id, [.. ids.Select(Row)]);
        // Every body read fails every attempt: a down body store, not a per-message fluke.
        foreach (var id in ids)
        {
            for (var attempt = 0; attempt < MigrationEngine.MaxBodyReadAttempts; attempt++)
            {
                source.QueueBodyAttempt(id, () => throw new InvalidOperationException("body store unreachable"));
            }
        }
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
    }
}
