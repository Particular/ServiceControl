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
class RetentionSweeper(
    ILogger<RetentionSweeper> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    IBodyStoragePersistence bodyStorage,
    RetentionMetrics metrics,
    RetentionSweepCustomCheck.State retentionState,
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

    // Read without the lock by status polling, so it is replaced whole rather than edited and a poll
    // never sees half of one run and half of another.
    volatile RetentionSweepCurrentStatus status = new(false, null, null, null, null, null, null);

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
#pragma warning disable PS0019 // Filtered on the token alone because SqlClient reports a cancelled command as a SqlException.
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Error during retention sweep");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping retention sweep");
        }
#pragma warning restore PS0019
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
            return new ManualSweepAttempt(RetentionSweepStatus.AlreadyRunning, status.LastStartedAt, errorCutoff, eventsCutoff);
        }

        // The background task releases the lock when the sweep ends, which SemaphoreSlim allows from
        // another thread. The run is marked started first so a status poll sees it straight away.
        var running = RecordStart(errorCutoff, eventsCutoff);

        _ = SweepWithoutAcquiringLock();

        return new ManualSweepAttempt(RetentionSweepStatus.Started, running.LastStartedAt, errorCutoff, eventsCutoff);

        async Task SweepWithoutAcquiringLock()
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, hostApplicationLifetime.ApplicationStopping);
            try
            {
                // if the caller doesn't hand over a real cancellation token then use the application lifetime.
                await SweepBody(running, errorCutoff, eventsCutoff, false, cancellation.Token);
            }
#pragma warning disable PS0019 // Filtered on the token alone because SqlClient reports a cancelled command as a SqlException.
            catch (Exception) when (cancellation.Token.IsCancellationRequested)
            {
                // Cancelled by the caller or by shutdown, which is not an error. The status records it.
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error during retention sweep");
            }
#pragma warning restore PS0019
        }
    }

    public RetentionSweepCurrentStatus GetStatus() => status;

    async Task Sweep(DateTime? errorCutoff, DateTime? eventsCutoff, bool pace, CancellationToken cancellationToken)
    {
        await sweepLock.WaitAsync(cancellationToken);
        var running = RecordStart(errorCutoff, eventsCutoff);
        await SweepBody(running, errorCutoff, eventsCutoff, pace, cancellationToken);
    }

    // A new snapshot leaves out the previous run's finish time and outcome so they are never read as this run's.
    RetentionSweepCurrentStatus RecordStart(DateTime? errorCutoff, DateTime? eventsCutoff)
    {
        var running = new RetentionSweepCurrentStatus(true, timeProvider.GetUtcNow().UtcDateTime, null, errorCutoff, eventsCutoff, null, null);
        status = running;
        return running;
    }

    // The lock is released before the run is shown as finished, so a caller who sees it finished can start
    // the next one. The compare-exchange keeps a run that has already started in that gap from being overwritten.
    void Finish(RetentionSweepCurrentStatus running, RetentionSweepOutcome outcome, List<string?> passErrors)
    {
        var errors = passErrors.OfType<string>().ToList();

        var finished = running with
        {
            IsRunning = false,
            LastFinishedAt = timeProvider.GetUtcNow().UtcDateTime,
            LastOutcome = outcome,
            LastError = errors.Count == 0 ? null : string.Join("; ", errors)
        };

        sweepLock.Release();
        Interlocked.CompareExchange(ref status, finished, running);
    }

    // The three sub-sweeps, shared by the hourly Sweep path and the manual background path. The caller
    // holds the lock, and every way out of here releases it.
    async Task SweepBody(RetentionSweepCurrentStatus running, DateTime? errorCutoff, DateTime? eventsCutoff, bool pace, CancellationToken cancellationToken)
    {
        List<string?> passErrors = [];
#pragma warning disable PS0021 // The pass lambdas' token is the cancellationToken that RunPass hands back, so there is only one token.
#pragma warning disable PS0019 // Filtered on the token alone because SqlClient reports a cancelled command as a SqlException.
        try
        {
            passErrors.Add(await RunPass(RetentionEntity.FailedMessages, token => SweepFailedMessages(pace, errorCutoff, token), cancellationToken));
            passErrors.Add(await RunPass(RetentionEntity.EventLog, token => SweepEventLogItems(pace, eventsCutoff, token), cancellationToken));
            passErrors.Add(await RunPass(RetentionEntity.GroupComments, SweepOrphanedGroupComments, cancellationToken));
            retentionState.SweepComplete();

            Finish(running, passErrors.Any(error => error is not null) ? RetentionSweepOutcome.Failed : RetentionSweepOutcome.Succeeded, passErrors);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            Finish(running, RetentionSweepOutcome.Cancelled, passErrors);
            throw;
        }
        catch (Exception ex)
        {
            passErrors.Add(FirstLine(ex.Message));
            Finish(running, RetentionSweepOutcome.Failed, passErrors);
            throw;
        }
#pragma warning restore PS0019
#pragma warning restore PS0021
    }

    // Each pass is isolated so one failing kind of row does not stop the others from being
    // reclaimed, and so the metrics report an outcome for every pass on every run.
    // Returns what went wrong for the status to report, or null when the pass succeeded.
    async Task<string?> RunPass(RetentionEntity entity, Func<CancellationToken, Task> pass, CancellationToken cancellationToken)
    {
        using var cycle = metrics.BeginCycle(entity, cancellationToken);

        try
        {
            await pass(cancellationToken);
            cycle.Complete();
            retentionState.Clear(entity);
            return null;
        }
#pragma warning disable PS0019 // Filtered on the token alone because SqlClient reports a cancelled command as a SqlException.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Error during the {RetentionEntity} retention pass", entity);
            retentionState.ReportError(entity, ex.Message);
            return $"{entity}: {FirstLine(ex.Message)}";
        }
#pragma warning restore PS0019
    }

    // Storage SDK messages can carry many lines of response detail, and the full text is already in the log.
    static string FirstLine(string message) => message.Split('\n', 2)[0].TrimEnd('\r');

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

        while (true)
        {
            // Throws rather than leaving the loop, so a pass cut short between batches is not reported as finished.
            cancellationToken.ThrowIfCancellationRequested();

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

        while (true)
        {
            // Throws rather than leaving the loop, so a pass cut short between batches is not reported as finished.
            cancellationToken.ThrowIfCancellationRequested();

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