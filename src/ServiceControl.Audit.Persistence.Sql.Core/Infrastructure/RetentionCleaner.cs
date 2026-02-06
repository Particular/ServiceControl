namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using System.Diagnostics;
using Abstractions;
using DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public abstract class RetentionCleaner(
    ILogger logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    AuditSqlPersisterSettings settings,
    IBodyStoragePersistence bodyPersistence,
    RetentionMetrics metrics,
    IngestionThrottleState throttleState) : BackgroundService
{
    protected const int BatchSize = 250;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting {ServiceName}", nameof(RetentionCleaner));

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            using PeriodicTimer timer = new(TimeSpan.FromHours(1), timeProvider);

            do
            {
                try
                {
                    await Clean(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to run retention cleaner");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping {ServiceName}", nameof(RetentionCleaner));
        }
    }

    async Task Clean(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContextBase>();

        var stopwatch = Stopwatch.StartNew();
        var cutoff = timeProvider.GetUtcNow().UtcDateTime - settings.AuditRetentionPeriod;

        var totalDeletedMessages = 0;
        var totalDeletedSnapshots = 0;
        var lockAcquired = false;

        using var cycleMetrics = metrics.BeginCleanupCycle();

        // Use execution strategy to handle retrying execution strategies that don't support user-initiated transactions
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Reset state on each retry attempt to avoid accumulating values across retries
            totalDeletedMessages = 0;
            totalDeletedSnapshots = 0;
            lockAcquired = false;

            await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

            if (!await TryAcquireLock(dbContext, stoppingToken))
            {
                logger.LogDebug("Another instance is running retention cleanup, skipping this cycle");
                metrics.RecordLockSkipped();
                return;
            }

            lockAcquired = true;

            // Signal cleanup starting - throttling will progressively increase over time
            throttleState.BeginCleanup();
            try
            {
                totalDeletedMessages = await CleanProcessedMessages(dbContext, cutoff, stoppingToken);
                totalDeletedSnapshots = await CleanSagaSnapshots(dbContext, cutoff, stoppingToken);
                await transaction.CommitAsync(stoppingToken);
            }
            finally
            {
                // Always restore full ingestion capacity when done
                throttleState.EndCleanup();
            }
        });

        if (lockAcquired)
        {
            cycleMetrics.Complete();

            logger.LogInformation("Retention cleanup removed {Messages} messages and {Snapshots} saga snapshots in {Elapsed}",
                totalDeletedMessages, totalDeletedSnapshots, stopwatch.Elapsed.ToString(@"hh\:mm\:ss"));
        }
    }

    async Task<int> CleanProcessedMessages(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken stoppingToken)
    {
        var totalDeleted = 0;
        int deleted;

        do
        {
            using var batchMetrics = metrics.BeginBatch(EntityTypes.Message);

            // Get a batch of IDs to delete so we can clean up body files
            // Note: No OrderBy - we just need any expired records, not necessarily the oldest first.
            // Ordering would require sorting potentially millions of rows and cause timeouts.
            var messageIdsToDelete = await dbContext.ProcessedMessages
                .Where(m => m.ProcessedAt < cutoff)
                .Select(m => m.UniqueMessageId)
                .Distinct()
                .Take(BatchSize)
                .ToListAsync(stoppingToken);

            if (messageIdsToDelete.Count == 0)
            {
                break;
            }

            // Delete body files first
            await bodyPersistence.DeleteBodies(messageIdsToDelete, stoppingToken);

            // Then delete the database records for this batch
            deleted = await dbContext.ProcessedMessages
                .Where(m => messageIdsToDelete.Contains(m.UniqueMessageId))
                .ExecuteDeleteAsync(stoppingToken);

            batchMetrics.RecordDeleted(deleted);
            totalDeleted += deleted;

            // Delay between batches to reduce contention with ingestion
            if (deleted == BatchSize)
            {
                await Task.Delay(settings.RetentionCleanupBatchDelay, stoppingToken);
            }
        } while (deleted >= BatchSize);

        return totalDeleted;
    }

    async Task<int> CleanSagaSnapshots(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken stoppingToken)
    {
        var totalDeleted = 0;
        int deleted;

        do
        {
            using var batchMetrics = metrics.BeginBatch(EntityTypes.SagaSnapshot);

            var snapshotIdsToDelete = await dbContext.SagaSnapshots
                .Where(s => s.ProcessedAt < cutoff)
                .Select(s => s.Id)
                .Take(BatchSize)
                .ToListAsync(stoppingToken);

            if (snapshotIdsToDelete.Count == 0)
            {
                break;
            }

            deleted = await dbContext.SagaSnapshots
                .Where(s => snapshotIdsToDelete.Contains(s.Id))
                .ExecuteDeleteAsync(stoppingToken);

            batchMetrics.RecordDeleted(deleted);
            totalDeleted += deleted;

            // Delay between batches to reduce contention with ingestion
            if (deleted == BatchSize)
            {
                await Task.Delay(settings.RetentionCleanupBatchDelay, stoppingToken);
            }
        } while (deleted >= BatchSize);

        return totalDeleted;
    }

    protected abstract Task<bool> TryAcquireLock(AuditDbContextBase dbContext, CancellationToken stoppingToken);
}
