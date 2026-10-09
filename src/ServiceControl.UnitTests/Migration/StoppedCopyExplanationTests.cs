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
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

// The copy can stop three ways that are not the engine's to report: the stall watchdog stopping one category,
// the host shutting down, and a second instance writing to the same database.
[TestFixture]
class StoppedCopyExplanationTests
{
    static readonly TimeSpan PollInterval = MigrationStartup.ClosedWindowProgress.PollInterval;
    static readonly TimeSpan StallLimit = MigrationStartup.ClosedWindowProgress.StallLimit;

    [Test]
    public async Task A_stall_settles_the_stalled_category_failed_and_the_next_category_still_runs()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new CancellationHonouringCheckpointStore();
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = new InMemoryMigrationTarget(store) { HangOnCall = 1, BeforeWrite = _ => writing.TrySetResult() };
        var source = new InMemoryMigrationSource();
        source.Seed(MigrationCategoryIds.KnownEndpoints, Row("KnownEndpoints-1"));
        // MessageRedirects does not follow KnownEndpoints, so a Failed KnownEndpoints cannot hold it back.
        source.Seed(MigrationCategoryIds.MessageRedirects, Row("MessageRedirects-1"));

        var run = Task.Run(() => MigrationStartup.RunRequiredCategories(
            Engine(source, target, store, clock), Categories(MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.MessageRedirects), store, clock, new CapturingLogger()));

        Assert.That(await clock.TimerCreated.WaitAsync(TimeSpan.FromSeconds(10)), Is.True, "the watchdog never started its timer, so advancing the clock would tick nothing");
        await writing.Task.WaitAsync(TimeSpan.FromSeconds(10));
        clock.Advance(StallLimit + PollInterval);
        var results = await run.WaitAsync(TimeSpan.FromSeconds(10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.Select(checkpoint => checkpoint.CategoryId), Is.EqualTo(new[] { MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.MessageRedirects }));
            Assert.That(results[0].State, Is.EqualTo(MigrationCategoryState.Halted), "a stalled category is Failed, so it waits for the operator rather than for the next start");
            Assert.That(results[0].SettledAt, Is.EqualTo(clock.GetUtcNow().UtcDateTime));
            Assert.That(results[0].LastError, Does.Contain($"committed nothing for {StallLimit.TotalMinutes:0.#} minutes").And.Not.Contain("--migration-"),
                "the row says what happened, and the refusal is what names the commands");
            Assert.That(await store.Read(MigrationCategoryIds.KnownEndpoints), Is.EqualTo(results[0]), "the stall was reported but never saved, so the next start would copy it again");
            Assert.That(results[1].State, Is.EqualTo(MigrationCategoryState.Complete), "one stalled category ended the whole start");
            Assert.That(target.WrittenRows(MigrationCategoryIds.MessageRedirects).Select(row => row.SourceId), Is.EqualTo(new[] { "MessageRedirects-1" }));
        }
    }

    [Test]
    public async Task A_host_stopping_mid_category_leaves_it_copying_and_the_stop_propagates()
    {
        var store = new InMemoryMigrationCheckpointStore();
        using var host = new CancellationTokenSource();
        var target = new InMemoryMigrationTarget(store) { DefaultBatchSize = 2, StopOnCall = (2, host) };
        var source = new InMemoryMigrationSource();
        source.Seed(MigrationCategoryIds.KnownEndpoints, Row("KnownEndpoints-1"), Row("KnownEndpoints-2"), Row("KnownEndpoints-3"), Row("KnownEndpoints-4"));
        source.Seed(MigrationCategoryIds.MessageRedirects, Row("MessageRedirects-1"));
        var clock = new FakeTimeProvider();

        var run = MigrationStartup.RunRequiredCategories(
            Engine(source, target, store, clock), Categories(MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.MessageRedirects), store, clock, new CapturingLogger(), host.Token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await run, Throws.InstanceOf<OperationCanceledException>(), "a shutdown is not the category's fault, so it must not come back as a settled row");

            var stopped = await store.Read(MigrationCategoryIds.KnownEndpoints);

            Assert.That(stopped!.State, Is.EqualTo(MigrationCategoryState.InProgress), "a shutdown settled Halted makes every restart refuse until the operator runs --migration-retry");
            Assert.That(stopped.Cursor, Is.EqualTo("KnownEndpoints-2"), "the next start resumes from the first batch's cursor");
            Assert.That(stopped.CopiedCount, Is.EqualTo(2));
            Assert.That(stopped.SettledAt, Is.Null);
            Assert.That(stopped.LastError, Is.Null);
            Assert.That((await store.Read(MigrationCategoryIds.MessageRedirects))!.State, Is.EqualTo(MigrationCategoryState.NotStarted), "a stopping host started the next category");
            Assert.That(target.RowsHandedToWrite(MigrationCategoryIds.MessageRedirects), Is.Empty);
        }
    }

    [Test]
    public async Task A_stale_in_progress_row_the_start_has_not_reached_is_not_judged_a_stall()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        var twoHoursAgo = clock.GetUtcNow().UtcDateTime - TimeSpan.FromHours(2);
        await store.Upsert(new MigrationCheckpoint(MigrationCategoryIds.EndpointSettings, MigrationCategoryState.InProgress, null, 0, 0, null, null, twoHoursAgo, twoHoursAgo, null, null));
        var target = new InMemoryMigrationTarget(store)
        {
            DefaultBatchSize = 1,
            // Each KnownEndpoints batch takes five minutes, and the next one waits until the watchdog has looked at the time.
            BeforeWrite = checkpoint =>
            {
                if (checkpoint.CategoryId == MigrationCategoryIds.KnownEndpoints)
                {
                    clock.Advance(TimeSpan.FromMinutes(5));
                    store.WaitForAPollAtTheCurrentTime().GetAwaiter().GetResult();
                }
            }
        };
        var source = new InMemoryMigrationSource();
        source.Seed(MigrationCategoryIds.KnownEndpoints, [.. Enumerable.Range(1, 8).Select(index => Row($"KnownEndpoints-{index}"))]);
        source.Seed(MigrationCategoryIds.EndpointSettings, Row("EndpointSettings-1"));

        var results = await Task.Run(() => MigrationStartup.RunRequiredCategories(
                Engine(source, target, store, clock), Categories(MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.EndpointSettings), store, clock, new CapturingLogger()))
            .WaitAsync(TimeSpan.FromSeconds(30));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.Select(checkpoint => checkpoint.State), Is.All.EqualTo(MigrationCategoryState.Complete),
                "forty minutes copying KnownEndpoints were judged against a row the start had not reached yet");
            Assert.That(target.WrittenRows(MigrationCategoryIds.KnownEndpoints), Has.Count.EqualTo(8));
            Assert.That(target.WrittenRows(MigrationCategoryIds.EndpointSettings).Select(row => row.SourceId), Is.EqualTo(new[] { "EndpointSettings-1" }));
        }
    }

    [Test]
    public void A_cancelled_copy_stays_a_cancellation() =>
        Assert.That(async () => await MigrationStartup.CopyOrExplainWhyItStopped(Task.FromCanceled<IReadOnlyList<MigrationCheckpoint>>(new CancellationToken(canceled: true)), NewSettings()),
            Throws.InstanceOf<OperationCanceledException>(),
            "a shutdown is not a failure, so it must not come out as a refusal telling the operator to go looking at the source and the target");

    [Test]
    public void A_checkpoint_saved_by_another_instance_says_which_instance_to_stop()
    {
        var conflict = new MigrationCheckpointConflictException("Checkpoint KnownEndpoints was saved from version 3, but the stored row is at version 4.");

        var exception = Assert.ThrowsAsync<Exception>(async () => await MigrationStartup.CopyOrExplainWhyItStopped(
            Task.FromException<IReadOnlyList<MigrationCheckpoint>>(conflict),
            NewSettings()));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("a second ServiceControl pointed at the same SQLServer database"),
                "the store's own message names a version, which tells nobody what is actually wrong");
            Assert.That(exception.Message, Does.Contain($"Stop the other instance, then restart with {MigrationSettings.EnabledKey} still on"),
                "this is the only refusal in the subsystem where doing nothing makes it worse");
            Assert.That(exception.Message, Does.Contain(conflict.Message).And.Contain("Nothing has opened on SQLServer yet"));
            Assert.That(exception.InnerException, Is.SameAs(conflict));
        });
    }

    [Test]
    public async Task A_copy_that_finished_is_returned_as_it_came_back()
    {
        IReadOnlyList<MigrationCheckpoint> copied = [new(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.Complete, null, 3, 0, null, null, null, null, null, null)];

        var finished = await MigrationStartup.CopyOrExplainWhyItStopped(Task.FromResult(copied), NewSettings());

        Assert.That(finished, Is.SameAs(copied));
    }

    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    static MigrationCategory[] Categories(params string[] categoryIds) => [.. categoryIds.Select(categoryId => MigrationCategoryRegistry.Find(categoryId)!)];

    static MigrationEngine Engine(InMemoryMigrationSource source, InMemoryMigrationTarget target, IMigrationCheckpointStore store, TimeProvider clock) =>
        new(source, target, store, clock, new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []), NullLogger<MigrationEngine>.Instance);

    static Settings NewSettings() =>
        new(transportType: "LearningTransport", persisterType: "SQLServer", errorRetentionPeriod: TimeSpan.FromDays(10));

    // Refuses a cancelled token as the EF Core store does, so a settle made on the stalled category's token fails here too.
    sealed class CancellationHonouringCheckpointStore : IMigrationCheckpointStore
    {
        readonly InMemoryMigrationCheckpointStore inner = new();

        public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return inner.ReadAll(cancellationToken);
        }

        public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return inner.Read(categoryId, cancellationToken);
        }

        public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return inner.Upsert(checkpoint, cancellationToken);
        }
    }
}
