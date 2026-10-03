#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

// No acceptance test can reach the watchdog: the host copies on the real clock and nobody waits half an hour.
[TestFixture]
class ClosedWindowProgressTests
{
    static readonly TimeSpan PollInterval = MigrationStartup.ClosedWindowProgress.PollInterval;
    static readonly TimeSpan StallLimit = MigrationStartup.ClosedWindowProgress.StallLimit;

    [Test]
    public async Task A_category_that_commits_nothing_for_the_stall_limit_has_its_copy_stopped()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints);

        clock.Advance(StallLimit + PollInterval);

        Assert.That(progress.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "a copy that committed nothing for longer than the limit was never stopped");
        await progress.DisposeAsync();
        Assert.That(progress.StalledCategoryId, Is.EqualTo(MigrationCategoryIds.KnownEndpoints), "the refusal message names the category from this");
    }

    [Test]
    public async Task A_resumed_row_carrying_the_previous_runs_stamp_is_not_a_stall()
    {
        // The row a killed run left behind keeps its last stamp, so an operator restarting hours later
        // would otherwise have a healthy copy cancelled on the first tick, every time.
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime - TimeSpan.FromHours(2)));
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints);

        clock.Advance(PollInterval);
        await WaitForAPollAtTheCurrentTime(store, clock);
        await progress.DisposeAsync();

        Assert.That(progress.StalledCategoryId, Is.Null, "the window runs from when this watch began, not from a stamp the previous run left");
    }

    [Test]
    public async Task A_row_left_running_by_another_run_is_not_this_copys_to_stop()
    {
        // ReadAll returns every row in the target, including ones this run never attempted.
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.EndpointSettings, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints);

        clock.Advance(StallLimit + PollInterval);
        await WaitForAPollAtTheCurrentTime(store, clock);
        await progress.DisposeAsync();

        Assert.That(progress.StalledCategoryId, Is.Null, "a category this run never attempted was named as the reason its copy stopped");
    }

    [Test]
    public async Task Committing_nothing_for_exactly_the_stall_limit_is_not_a_stall()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints);

        clock.Advance(StallLimit);
        await WaitForAPollAtTheCurrentTime(store, clock);
        await progress.DisposeAsync();

        Assert.That(progress.StalledCategoryId, Is.Null, "the limit is the point at which a copy has not yet stalled");
    }

    [Test]
    public async Task A_category_that_has_committed_nothing_since_this_watch_began_is_stopped()
    {
        // The engine stamps a category when it marks it running, so the window covers counting the source
        // and the first read, which is where a copy that never gets going actually hangs.
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime, startedAt: clock.GetUtcNow().UtcDateTime - TimeSpan.FromDays(3)));
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints);

        clock.Advance(StallLimit + PollInterval);

        Assert.That(progress.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "a category that has committed nothing since the watch began was never stopped");
        await progress.DisposeAsync();
        Assert.That(progress.StalledCategoryId, Is.EqualTo(MigrationCategoryIds.KnownEndpoints));
    }

    [Test]
    public async Task A_row_an_older_build_left_without_a_stamp_is_still_watched()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: null, startedAt: clock.GetUtcNow().UtcDateTime - TimeSpan.FromDays(3)));
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints);

        clock.Advance(StallLimit + PollInterval);

        Assert.That(progress.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "an unstamped row was left unwatched for ever");
        await progress.DisposeAsync();
        Assert.That(progress.StalledCategoryId, Is.EqualTo(MigrationCategoryIds.KnownEndpoints));
    }

    [Test]
    public async Task A_category_this_run_finished_is_not_stopped_for_having_gone_quiet()
    {
        // Required categories run one at a time, so a small one settles early and its stamp then ages
        // for as long as the next category takes.
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(Settled(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.Complete, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints);

        clock.Advance(StallLimit + PollInterval);
        await WaitForAPollAtTheCurrentTime(store, clock);
        await progress.DisposeAsync();

        Assert.That(progress.StalledCategoryId, Is.Null, "a category that finished was named as the reason the copy stopped");
    }

    [Test]
    public async Task A_halted_category_this_run_attempted_is_not_stopped_for_having_gone_quiet()
    {
        // A halt settles the row and leaves its stamp behind. The gate is what refuses the host over it, not the watchdog.
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(Settled(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.Halted, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints);

        clock.Advance(StallLimit + PollInterval);
        await WaitForAPollAtTheCurrentTime(store, clock);
        await progress.DisposeAsync();

        Assert.That(progress.StalledCategoryId, Is.Null, "a halted category was named as the reason the copy stopped");
    }

    // The watch has no total limit, so a copy that keeps committing outlives any length of run.
    [Test]
    public async Task A_category_still_committing_batches_is_never_stopped_however_long_it_takes()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        var committed = await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints);

        // Four times the limit, committing a batch every poll, which a total timeout would have killed long ago.
        for (var elapsed = TimeSpan.Zero; elapsed < StallLimit * 4; elapsed += PollInterval)
        {
            clock.Advance(PollInterval);
            await WaitForAPollAtTheCurrentTime(store, clock);
            committed = await store.Upsert(committed with { LastProgressAt = clock.GetUtcNow().UtcDateTime });
        }

        await progress.DisposeAsync();

        Assert.That(progress.StalledCategoryId, Is.Null, "a copy committing a batch every poll was stopped, so the watch is a deadline rather than a stall detector");
    }

    [Test]
    public async Task A_poll_that_fails_leaves_the_watch_running_and_says_so()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        var storeFailure = new InvalidOperationException("the checkpoint store is unreachable");
        store.FailReadAll(1, storeFailure);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        var logger = new CapturingLogger();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, logger);

        clock.Advance(PollInterval);
        await WaitForAPollAtTheCurrentTime(store, clock);
        clock.Advance(StallLimit + PollInterval);

        Assert.That(progress.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "a watch that stopped at the first blip never noticed the stall that followed");
        await progress.DisposeAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(progress.StalledCategoryId, Is.EqualTo(MigrationCategoryIds.KnownEndpoints));
            Assert.That(logger.Entries.Where(entry => entry.Level == LogLevel.Error).Select(entry => entry.Exception), Has.Member(storeFailure), "a watch that has gone deaf has to say so");
        }
    }

    static async Task<MigrationStartup.ClosedWindowProgress> StartWatching(PollObservingCheckpointStore store, TimerRecordingTimeProvider clock, string attemptedCategoryId, ILogger? logger = null)
    {
        var progress = new MigrationStartup.ClosedWindowProgress(store, clock, logger ?? new CapturingLogger(), [attemptedCategoryId]);

        Assert.That(await clock.TimerCreated.WaitAsync(TimeSpan.FromSeconds(10)), Is.True, "the watch never started its timer, so advancing the clock would tick nothing");

        return progress;
    }

    // Without this, a test asserting the watch did nothing would just be asking before the watch had looked.
    static async Task WaitForAPollAtTheCurrentTime(PollObservingCheckpointStore store, TimerRecordingTimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        while (true)
        {
            Assert.That(await store.Polled.WaitAsync(TimeSpan.FromSeconds(10)), Is.True, $"no poll read the checkpoints at {now:O}; a watch that stopped polling never reaches one");

            if (store.PolledAt.TryDequeue(out var polledAt) && polledAt == now)
            {
                return;
            }
        }
    }

    static MigrationCheckpoint InProgress(string categoryId, DateTime? lastProgressAt, DateTime? startedAt = null) =>
        new(categoryId, MigrationCategoryState.InProgress, null, 0, 0, null, null, startedAt ?? lastProgressAt, lastProgressAt, null, null);

    static MigrationCheckpoint Settled(string categoryId, MigrationCategoryState state, DateTime lastProgressAt) =>
        new(categoryId, state, null, 0, 0, null, null, lastProgressAt, lastProgressAt, lastProgressAt, null);

    // Records when each poll read the rows, so a test can wait for a poll that saw the state it is judging.
    sealed class PollObservingCheckpointStore(TimeProvider clock) : IMigrationCheckpointStore
    {
        readonly InMemoryMigrationCheckpointStore inner = new();
        Exception? readAllFailure;
        int readAllFailuresLeft;

        public SemaphoreSlim Polled { get; } = new(0);

        public ConcurrentQueue<DateTime> PolledAt { get; } = new();

        public void FailReadAll(int times, Exception failure)
        {
            readAllFailuresLeft = times;
            readAllFailure = failure;
        }

        public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default)
        {
            PolledAt.Enqueue(clock.GetUtcNow().UtcDateTime);
            Polled.Release();

            if (readAllFailuresLeft > 0)
            {
                readAllFailuresLeft--;
                return Task.FromException<IReadOnlyList<MigrationCheckpoint>>(readAllFailure!);
            }

            return inner.ReadAll(cancellationToken);
        }

        public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) => inner.Read(categoryId, cancellationToken);

        public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default) => inner.Upsert(checkpoint, cancellationToken);
    }
}
