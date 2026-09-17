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
        source.FailBodyReads("msg-1", MigrationEngine.MaxBodyReadAttempts - 1, new TimeoutException("blip"));
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
            Assert.That(source.BodyReadAttempts("msg-1"), Is.EqualTo(MigrationEngine.MaxBodyReadAttempts));
        }
    }

    // Every type the engine treats as a defect. Each fails the same way on every attempt, so retrying one
    // would only turn a code fault into skipped messages.
    static readonly Exception[] Defects =
    [
        new NotSupportedException("this source cannot read bodies"),
        new NotImplementedException("not written yet"),
        new InvalidOperationException("the session is closed"),
        new ArgumentException("the id is not a document id"),
        new NullReferenceException("no attachment"),
        new InvalidCastException("not an attachment")
    ];

    [TestCaseSource(nameof(Defects))]
    public async Task A_body_read_that_fails_as_a_defect_halts_the_category_on_the_first_attempt_without_skipping_the_message(Exception defect)
    {
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1"));
        source.FailBodyReads("msg-1", MigrationEngine.MaxBodyReadAttempts, defect);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That(checkpoint.LastError, Does.Contain(defect.GetType().Name));
            Assert.That(source.BodyReadAttempts("msg-1"), Is.EqualTo(1));
            Assert.That(checkpoint.SkippedCount, Is.Zero);
            Assert.That(checkpoint.SkipReasons, Is.Null);
        }
    }

    [Test]
    public async Task A_shutdown_during_a_body_read_stops_the_run_instead_of_skipping_the_message()
    {
        // Retrying a shutdown to the attempt limit and then recording the message as permanently
        // unreadable is the one path here that silently loses a customer's failed message.
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1"));
        using var stopping = new CancellationTokenSource();
        source.StopOnBodyRead = ("msg-1", stopping);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        Assert.ThrowsAsync<OperationCanceledException>(() => engine.RunCategoryAsync(category, stopping.Token));

        var persisted = await checkpointStore.Read(category.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.BodyReadAttempts("msg-1"), Is.EqualTo(1), "a shutdown is not a transient body failure, so it is not retried");
            Assert.That(persisted!.State, Is.EqualTo(MigrationCategoryState.InProgress));
            Assert.That(persisted.SkippedCount, Is.Zero, "the message is still there to copy on the next run");
            Assert.That(target.WrittenRows(category.Id), Is.Empty);
        }
    }

    [Test]
    public async Task The_configured_backoff_is_waited_between_body_read_attempts()
    {
        // Without the wait, three attempts against a body store that is briefly down all fail inside a
        // millisecond and the message is skipped for an outage it would have survived.
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1"));
        var body = new MigrationBody(new byte[] { 1 }, "text/plain");
        source.SetBody("msg-1", body);
        source.FailBodyReads("msg-1", MigrationEngine.MaxBodyReadAttempts - 1, new TimeoutException("body store unreachable"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var clock = new TimerRecordingTimeProvider();
        var backoff = TimeSpan.FromMilliseconds(200);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []) { BodyRetryBackoff = backoff };
        var engine = new MigrationEngine(source, target, checkpointStore, clock, options, NullLogger<MigrationEngine>.Instance);

        var runTask = engine.RunCategoryAsync(category);

        // Two failures, so a wait after each before the attempt that succeeds.
        for (var waitNumber = 1; waitNumber <= MigrationEngine.MaxBodyReadAttempts - 1; waitNumber++)
        {
            Assert.That(await clock.TimerCreated.WaitAsync(TimeSpan.FromSeconds(5)), Is.True, $"backoff {waitNumber} never started");
            Assert.That(runTask.IsCompleted, Is.False, $"backoff {waitNumber} should still be pending");
            clock.Advance(backoff);
        }

        var checkpoint = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(clock.DueTimes, Is.EqualTo(new[] { backoff, backoff }), "no wait after the final attempt, which has nothing left to retry");
            Assert.That(target.WrittenRows(category.Id).Single().Body, Is.EqualTo(body));
        }
    }

    [Test]
    public async Task The_skip_warning_for_an_unreadable_body_carries_the_last_attempts_exception()
    {
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1"));
        var failure = new TimeoutException("body store unreachable");
        source.FailBodyReads("msg-1", MigrationEngine.MaxBodyReadAttempts, failure);
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore);
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var logger = new CapturingLogger();
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, logger);

        await engine.RunCategoryAsync(category);

        Assert.That(logger.Entries.Single(e => e.Message.StartsWith("Skipped msg-1")).Exception, Is.SameAs(failure));
    }

    [Test]
    public async Task A_body_the_source_already_attached_is_written_without_reading_it_again()
    {
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        var attached = new MigrationBody(new byte[] { 7 }, "text/plain");
        source.Seed(category.Id, Row("msg-1") with { Body = attached });
        source.FailBodyReads("msg-1", MigrationEngine.MaxBodyReadAttempts, new TimeoutException("a body read nobody needed"));
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
        source.FailBodyReads("msg-1", MigrationEngine.MaxBodyReadAttempts, new TimeoutException("down"));
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
            source.FailBodyReads(id, MigrationEngine.MaxBodyReadAttempts, new TimeoutException("body store unreachable"));
        }
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 100 };
        var options = new MigrationEngineOptions(TimeSpan.Zero, HaltThresholdPercent: 5, HaltThresholdMinimum: 100, []) { BodyRetryBackoff = TimeSpan.Zero };
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Halted));
    }
}
