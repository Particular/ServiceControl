#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
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
    public async Task A_category_that_commits_nothing_for_the_stall_limit_is_stopped()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop);

        clock.Advance(StallLimit + PollInterval);

        Assert.That(stop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "a category that committed nothing for longer than the limit was never stopped");
        await progress.DisposeAsync();
    }

    // A call that ignores its token never returns, so the row stays in progress and every later poll sees the same stall.
    [Test]
    public async Task A_category_already_stopped_is_reported_once_however_long_it_stays_stuck()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        var logger = new CapturingLogger();
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop, logger: logger);

        clock.Advance(StallLimit + PollInterval);
        await store.WaitForAPollAtTheCurrentTime();
        clock.Advance(PollInterval);
        await store.WaitForAPollAtTheCurrentTime();
        await progress.DisposeAsync();

        Assert.That(logger.Entries.Count(entry => entry.Level == LogLevel.Error), Is.EqualTo(1), "the same stall was reported again on the next poll");
    }

    [Test]
    public async Task A_resumed_row_carrying_the_previous_runs_stamp_is_not_a_stall()
    {
        // The row a killed run left behind keeps its last stamp, so an operator restarting hours later
        // would otherwise have a healthy copy cancelled on the first tick, every time.
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime - TimeSpan.FromHours(2)));
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop);

        clock.Advance(PollInterval);
        await store.WaitForAPollAtTheCurrentTime();
        await progress.DisposeAsync();

        Assert.That(stop.IsCancellationRequested, Is.False, "the window runs from when this category's run began, not from a stamp the previous run left");
    }

    [Test]
    public async Task A_row_other_than_the_running_category_is_not_judged()
    {
        // Both were attempted, but only KnownEndpoints is running, and it has no row yet.
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.EndpointSettings, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop, [MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.EndpointSettings]);

        clock.Advance(StallLimit + PollInterval);
        await store.WaitForAPollAtTheCurrentTime();
        await progress.DisposeAsync();

        Assert.That(stop.IsCancellationRequested, Is.False, "the running category was stopped over a row nobody is copying");
    }

    [Test]
    public async Task Committing_nothing_for_exactly_the_stall_limit_is_not_a_stall()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop);

        clock.Advance(StallLimit);
        await store.WaitForAPollAtTheCurrentTime();
        await progress.DisposeAsync();

        Assert.That(stop.IsCancellationRequested, Is.False, "the limit is the point at which a copy has not yet stalled");
    }

    [Test]
    public async Task A_category_that_has_committed_nothing_since_its_run_began_is_stopped()
    {
        // The engine stamps a category when it marks it running, so the window covers the first read, which is
        // where a copy that never gets going actually hangs. When the category first started does not matter.
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime, startedAt: clock.GetUtcNow().UtcDateTime - TimeSpan.FromDays(3)));
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop);

        clock.Advance(StallLimit + PollInterval);

        Assert.That(stop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "a category that has committed nothing since its run began was never stopped");
        await progress.DisposeAsync();
    }

    [Test]
    public async Task A_row_an_older_build_left_without_a_stamp_is_still_watched()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: null, startedAt: clock.GetUtcNow().UtcDateTime - TimeSpan.FromDays(3)));
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop);

        clock.Advance(StallLimit + PollInterval);

        Assert.That(stop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "an unstamped row was left unwatched for ever");
        await progress.DisposeAsync();
    }

    [Test]
    public async Task A_category_this_run_finished_is_not_stopped_for_having_gone_quiet()
    {
        // The watch moves to the next category only when that one starts, so a settled row can still be the one watched.
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(Settled(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.Complete, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop);

        clock.Advance(StallLimit + PollInterval);
        await store.WaitForAPollAtTheCurrentTime();
        await progress.DisposeAsync();

        Assert.That(stop.IsCancellationRequested, Is.False, "a category that finished was stopped");
    }

    [Test]
    public async Task A_halted_category_this_run_attempted_is_not_stopped_for_having_gone_quiet()
    {
        // A halt settles the row and leaves its stamp behind. The gate is what refuses the host over it, not the watchdog.
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        await store.Upsert(Settled(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.Halted, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop);

        clock.Advance(StallLimit + PollInterval);
        await store.WaitForAPollAtTheCurrentTime();
        await progress.DisposeAsync();

        Assert.That(stop.IsCancellationRequested, Is.False, "a halted category was stopped");
    }

    // The watch has no total limit, so a copy that keeps committing outlives any length of run.
    [Test]
    public async Task A_category_still_committing_batches_is_never_stopped_however_long_it_takes()
    {
        var clock = new TimerRecordingTimeProvider();
        var store = new PollObservingCheckpointStore(clock);
        var committed = await store.Upsert(InProgress(MigrationCategoryIds.KnownEndpoints, lastProgressAt: clock.GetUtcNow().UtcDateTime));
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop);

        // Four times the limit, committing a batch every poll, which a total timeout would have killed long ago.
        for (var elapsed = TimeSpan.Zero; elapsed < StallLimit * 4; elapsed += PollInterval)
        {
            clock.Advance(PollInterval);
            await store.WaitForAPollAtTheCurrentTime();
            committed = await store.Upsert(committed with { LastProgressAt = clock.GetUtcNow().UtcDateTime });
        }

        await progress.DisposeAsync();

        Assert.That(stop.IsCancellationRequested, Is.False, "a copy committing a batch every poll was stopped, so the watch is a deadline rather than a stall detector");
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
        using var stop = new CancellationTokenSource();
        var progress = await StartWatching(store, clock, MigrationCategoryIds.KnownEndpoints, stop, logger: logger);

        clock.Advance(PollInterval);
        await store.WaitForAPollAtTheCurrentTime();
        clock.Advance(StallLimit + PollInterval);

        Assert.That(stop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "a watch that stopped at the first blip never noticed the stall that followed");
        await progress.DisposeAsync();
        Assert.That(logger.Entries.Where(entry => entry.Level == LogLevel.Error).Select(entry => entry.Exception), Has.Member(storeFailure), "a watch that has gone deaf has to say so");
    }

    static async Task<MigrationStartup.ClosedWindowProgress> StartWatching(PollObservingCheckpointStore store, TimerRecordingTimeProvider clock, string runningCategoryId, CancellationTokenSource stop, string[]? attemptedCategoryIds = null, ILogger? logger = null)
    {
        var progress = new MigrationStartup.ClosedWindowProgress(store, clock, logger ?? new CapturingLogger(), attemptedCategoryIds ?? [runningCategoryId]);
        progress.Watch(runningCategoryId, clock.GetUtcNow().UtcDateTime, stop);

        Assert.That(await clock.TimerCreated.WaitAsync(TimeSpan.FromSeconds(10)), Is.True, "the watch never started its timer, so advancing the clock would tick nothing");

        return progress;
    }

    static MigrationCheckpoint InProgress(string categoryId, DateTime? lastProgressAt, DateTime? startedAt = null) =>
        new(categoryId, MigrationCategoryState.InProgress, null, 0, 0, null, null, startedAt ?? lastProgressAt, lastProgressAt, null, null);

    static MigrationCheckpoint Settled(string categoryId, MigrationCategoryState state, DateTime lastProgressAt) =>
        new(categoryId, state, null, 0, 0, null, null, lastProgressAt, lastProgressAt, lastProgressAt, null);
}
