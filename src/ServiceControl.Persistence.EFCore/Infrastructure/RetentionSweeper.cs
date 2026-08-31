namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure.Metrics;

// Deletes rows once they age past their retention period.
// Runs hourly, in bounded batches so it never holds a large delete, and recomputes the cutoffs on
// every run so a changed retention setting takes effect without rewriting any row.
public class RetentionSweeper(
    ILogger<RetentionSweeper> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    IBodyStoragePersistence bodyStorage,
    RetentionMetrics metrics,
    EFPersisterSettings settings) : BackgroundService
{
    const int BatchSize = 1000;
    static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    static readonly TimeSpan BatchPause = TimeSpan.FromSeconds(1);

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
                    await Sweep(pace: true, cancellationToken);
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
    // Intended for tests that need the effect without waiting for the hourly loop.
    public Task SweepNow(CancellationToken cancellationToken = default) => Sweep(pace: false, cancellationToken);

    async Task Sweep(bool pace, CancellationToken cancellationToken)
    {
        await RunPass(RetentionEntity.FailedMessages, token => SweepFailedMessages(pace, token), cancellationToken);
        await RunPass(RetentionEntity.EventLog, token => SweepEventLogItems(pace, token), cancellationToken);
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
    // ordered DELETE.
    async Task SweepEventLogItems(bool pace, CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime - settings.EventsRetentionPeriod;

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

    async Task SweepFailedMessages(bool pace, CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime - settings.ErrorRetentionPeriod;

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
