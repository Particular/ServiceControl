namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure.Metrics;

// Deletes rows once they age past their retention period.
// Runs hourly, in bounded batches so it never holds a large delete, and recomputes the cutoffs on
// every run so a changed retention setting takes effect without rewriting any row.
//
// A manual sweep can be triggered via the API (see IRetentionSweeper / IRetentionApi) with
// caller-supplied cutoffs. 
public class RetentionSweeper(
    ILogger<RetentionSweeper> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    IBodyStoragePersistence bodyStorage,
    RetentionMetrics metrics,
    EFPersisterSettings settings,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService, IRetentionSweeper
{
    const int BatchSize = 1000;
    static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    static readonly TimeSpan BatchPause = TimeSpan.FromSeconds(1);

    // Single-flight guard shared by the hourly timer path and the manual API path so two sweeps
    // never overlap. Precedent: ExternalIntegrationRequestsDataStore.drainLock.
    readonly SemaphoreSlim sweepLock = new(1, 1);

    // Status snapshot for GET /api/retention/sweep/status polling. Volatile reads/writes are
    // sufficient here: the fields are written under sweepLock (or once at start) and read
    // lock-free for status reporting, which only needs an eventually-consistent snapshot.
    volatile bool isRunning;
    DateTime? lastStartedAt;
    DateTime? lastFinishedAt;
    DateTime? lastErrorCutoff;
    DateTime? lastEventsCutoff;
    string? lastError;

    public RetentionSweepConfig Config => new(settings.ErrorRetentionPeriod, settings.EventsRetentionPeriod);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting retention sweep");

        try
        {
            await Task.Delay(InitialDelay, timeProvider, cancellationToken);

            using PeriodicTimer timer = new(Interval, timeProvider);

            do
            {
                try
                {
                    await Sweep(errorCutoff: null, eventsCutoff: null, pace: true, cancellationToken);
                }
#pragma warning disable PS0019 // The filter already excludes OperationCanceledException, so
                // cancellation propagates; PS0019 only recognises a cancellationToken guard.
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error during retention sweep");
                }
#pragma warning restore PS0019
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping retention sweep");
        }
    }

    // Runs a full sweep immediately, bypassing the timer and the inter-batch pause.
    // Intended for tests that need the effect without waiting for the hourly loop. Uses the
    // default cutoff derivation (now - retention period).
    public Task SweepNow(CancellationToken cancellationToken = default) =>
        Sweep(errorCutoff: null, eventsCutoff: null, pace: false, cancellationToken);

    public ManualSweepAttempt TryStartManualSweep(DateTime? errorCutoff, DateTime? eventsCutoff, CancellationToken cancellationToken = default)
    {
        // Try to acquire the single-flight lock without waiting if a scheduled or manual sweep is
        // already running (holding the lock)
        if (!sweepLock.Wait(0, cancellationToken))
        {
            return new ManualSweepAttempt(ManualSweepOutcome.AlreadyRunning, lastStartedAt, errorCutoff, eventsCutoff);
        }

        // Lock acquired on this thread. The background task owns it from here and releases it when
        // the sweep body completes (SemaphoreSlim is not thread-affine, so releasing from the
        // background thread is safe). isRunning is set now so a concurrent manual call sees it.
        isRunning = true;
        lastStartedAt = timeProvider.GetUtcNow().UtcDateTime;
        lastErrorCutoff = errorCutoff;
        lastEventsCutoff = eventsCutoff;
        lastError = null;

        _ = SweepWithoutAcquiringLock();

        return new ManualSweepAttempt(ManualSweepOutcome.Started, lastStartedAt, errorCutoff, eventsCutoff);

        async Task SweepWithoutAcquiringLock()
        {
            try
            {
                // if the caller doesn't hand over a real cancellation token then use the application lifetime.
                await SweepBody(errorCutoff, eventsCutoff, false, cancellationToken.CanBeCanceled ? cancellationToken : hostApplicationLifetime.ApplicationStopping);
                lastFinishedAt = timeProvider.GetUtcNow().UtcDateTime;
            }
            finally
            {
                isRunning = false;
                sweepLock.Release();
            }
        }
    }

    public RetentionSweepStatus GetStatus() => new(isRunning, lastStartedAt, lastFinishedAt, lastErrorCutoff, lastEventsCutoff, lastError);

    async Task Sweep(DateTime? errorCutoff, DateTime? eventsCutoff, bool pace, CancellationToken cancellationToken)
    {
        await sweepLock.WaitAsync(cancellationToken);
        isRunning = true;
        lastStartedAt = timeProvider.GetUtcNow().UtcDateTime;
        lastErrorCutoff = errorCutoff;
        lastEventsCutoff = eventsCutoff;
        lastError = null;
        try
        {
            await SweepBody(errorCutoff, eventsCutoff, pace, cancellationToken);
            lastFinishedAt = timeProvider.GetUtcNow().UtcDateTime;
        }
        finally
        {
            isRunning = false;
            sweepLock.Release();
        }
    }

    // The three sub-sweeps, isolated from lock management so both the locked Sweep path and the
    // manual background path (which already holds the lock) share one implementation.
    async Task SweepBody(DateTime? errorCutoff, DateTime? eventsCutoff, bool pace, CancellationToken cancellationToken)
    {
        await RunPass(RetentionEntity.FailedMessages, token => SweepFailedMessages(pace, errorCutoff, token), cancellationToken);
        await RunPass(RetentionEntity.EventLog, token => SweepEventLogItems(pace, eventsCutoff, token), cancellationToken);
        await RunPass(RetentionEntity.GroupComments, SweepOrphanedGroupComments, cancellationToken);
    }

    // Each pass is isolated so one failing kind of row does not stop the others from being
    // reclaimed, and so the metrics report an outcome for every pass on every run.
    async Task RunPass(RetentionEntity entity, Func<CancellationToken, Task> pass, CancellationToken cancellationToken)
    {
        using var cycle = metrics.BeginCycle(entity, cancellationToken);

        try
        {
            await pass(cancellationToken);

            cycle.Complete();
        }
#pragma warning disable PS0019 // The filter already excludes OperationCanceledException, so
        // cancellation propagates; PS0019 only recognises a cancellationToken guard.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error during the {RetentionEntity} retention pass", entity);
        }
#pragma warning restore PS0019
    }

    // Once the last message of a group has been swept the group cannot be displayed at all, so its
    // comment is unreachable. Leaving it behind would both accumulate invisible rows and, because
    // group ids are deterministic, reattach a stale comment if the same failure ever recurs.
    async Task SweepOrphanedGroupComments(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        var deleted = await dbContext.GroupComments
            .Where(comment => !dbContext.FailedMessageGroups.Any(group => group.GroupId == comment.GroupId))
            .ExecuteDeleteAsync(cancellationToken);

        metrics.RecordRowsDeleted(RetentionEntity.GroupComments, deleted);
    }

    // Event log items are insert-only and carry no external bodies, so each batch is a single
    // ordered DELETE. A caller-supplied cutoff overrides the default derivation.
    async Task SweepEventLogItems(bool pace, DateTime? eventsCutoff, CancellationToken cancellationToken)
    {
        var cutoff = eventsCutoff ?? (timeProvider.GetUtcNow().UtcDateTime - settings.EventsRetentionPeriod);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            var deleted = await dbContext.EventLogItems
                .Where(eventLogItem => eventLogItem.RaisedAt < cutoff)
                .OrderBy(eventLogItem => eventLogItem.RaisedAt)
                .Take(BatchSize)
                .ExecuteDeleteAsync(cancellationToken);

            metrics.RecordRowsDeleted(RetentionEntity.EventLog, deleted);

            if (deleted < BatchSize)
            {
                break;
            }

            if (pace)
            {
                await Task.Delay(BatchPause, timeProvider, cancellationToken);
            }
        }
    }

    async Task SweepFailedMessages(bool pace, DateTime? errorCutoff, CancellationToken cancellationToken)
    {
        var cutoff = errorCutoff ?? (timeProvider.GetUtcNow().UtcDateTime - settings.ErrorRetentionPeriod);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            var expired = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(IsExpired(cutoff))
                .OrderBy(failedMessage => failedMessage.StatusChangedAt)
                .Take(BatchSize)
                .Select(failedMessage => new { failedMessage.UniqueMessageId, failedMessage.BodyStoredExternally })
                .ToListAsync(cancellationToken);

            if (expired.Count == 0)
            {
                // The other two passes always report what their delete removed, so this one
                // reports its zero rather than leaving a gap in the series.
                metrics.RecordRowsDeleted(RetentionEntity.FailedMessages, 0);
                break;
            }

            // External bodies are deleted before the rows, so a body that will not delete fails the
            // pass with its row intact and the next sweep retries it. Every store already treats an
            // already-missing body as a success, so anything reaching here is a storage failure and
            // deleting the row would strand the body with nothing left to name it.
            foreach (var row in expired.Where(row => row.BodyStoredExternally))
            {
                await bodyStorage.DeleteBodyIfExists(row.UniqueMessageId.ToString(), cancellationToken);
            }

            var ids = expired.Select(row => row.UniqueMessageId).ToArray();

            // The predicate is re-asserted so a message that was re-failed (back to Unresolved)
            // between the select and the delete is left alone. The cascade removes its group rows.
            var deleted = await dbContext.FailedMessages
                .Where(failedMessage => ids.Contains(failedMessage.UniqueMessageId))
                .Where(IsExpired(cutoff))
                .ExecuteDeleteAsync(cancellationToken);

            metrics.RecordRowsDeleted(RetentionEntity.FailedMessages, deleted);

            if (expired.Count < BatchSize)
            {
                break;
            }

            if (pace)
            {
                await Task.Delay(BatchPause, timeProvider, cancellationToken);
            }
        }
    }

    static System.Linq.Expressions.Expression<Func<FailedMessageEntity, bool>> IsExpired(DateTime cutoff) =>
        failedMessage => (failedMessage.Status == FailedMessageStatus.Resolved || failedMessage.Status == FailedMessageStatus.Archived)
            && failedMessage.StatusChangedAt < cutoff;
}