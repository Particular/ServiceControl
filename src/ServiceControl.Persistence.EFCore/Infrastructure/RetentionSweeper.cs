namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

// Deletes rows once they age past their retention period.
// Runs hourly, in bounded batches so it never holds a large delete, and recomputes the cutoffs on
// every run so a changed retention setting takes effect without rewriting any row.
public class RetentionSweeper(
    ILogger<RetentionSweeper> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    IBodyStoragePersistence bodyStorage,
    EFPersisterSettings settings) : BackgroundService
{
    const int BatchSize = 1000;
    static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    static readonly TimeSpan BatchPause = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting retention sweep");

        try
        {
            await Task.Delay(InitialDelay, timeProvider, stoppingToken);

            using PeriodicTimer timer = new(Interval, timeProvider);

            do
            {
                try
                {
                    await Sweep(pace: true, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error during retention sweep");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping retention sweep");
        }
    }

    // Runs a full sweep immediately, bypassing the timer and the inter-batch pause.
    // Intended for tests that need the effect without waiting for the hourly loop.
    public Task SweepNow(CancellationToken cancellationToken = default) => Sweep(pace: false, cancellationToken);

    async Task Sweep(bool pace, CancellationToken cancellationToken)
    {
        await SweepFailedMessages(pace, cancellationToken);
        await SweepEventLogItems(pace, cancellationToken);
        await SweepOrphanedGroupComments(cancellationToken);
    }

    // Once the last message of a group has been swept the group cannot be displayed at all, so its
    // comment is unreachable. Leaving it behind would both accumulate invisible rows and, because
    // group ids are deterministic, reattach a stale comment if the same failure ever recurs.
    async Task SweepOrphanedGroupComments(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        await dbContext.GroupComments
            .Where(comment => !dbContext.FailedMessageGroups.Any(group => group.GroupId == comment.GroupId))
            .ExecuteDeleteAsync(cancellationToken);
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
                break;
            }

            // External bodies are deleted before the rows. A crash in between leaves rows the next
            // sweep re-handles (tolerating the already-missing body); deleting rows first would
            // instead leak the external bodies.
            foreach (var row in expired.Where(row => row.BodyStoredExternally))
            {
                await DeleteExternalBody(row.UniqueMessageId, cancellationToken);
            }

            var ids = expired.Select(row => row.UniqueMessageId).ToArray();

            // The predicate is re-asserted so a message that was re-failed (back to Unresolved)
            // between the select and the delete is left alone. The cascade removes its group rows.
            await dbContext.FailedMessages
                .Where(failedMessage => ids.Contains(failedMessage.UniqueMessageId))
                .Where(IsExpired(cutoff))
                .ExecuteDeleteAsync(cancellationToken);

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

    async Task DeleteExternalBody(Guid uniqueMessageId, CancellationToken cancellationToken)
    {
        try
        {
            await bodyStorage.DeleteBody(uniqueMessageId.ToString(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Retention must not stall on a missing or unavailable body.
            logger.LogWarning(ex, "Could not delete the external body for {UniqueMessageId} during retention", uniqueMessageId);
        }
    }

    static System.Linq.Expressions.Expression<Func<FailedMessageEntity, bool>> IsExpired(DateTime cutoff) =>
        failedMessage => (failedMessage.Status == FailedMessageStatus.Resolved || failedMessage.Status == FailedMessageStatus.Archived)
            && failedMessage.StatusChangedAt < cutoff;
}
