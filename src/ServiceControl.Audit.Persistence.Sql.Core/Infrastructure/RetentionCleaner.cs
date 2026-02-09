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

        using var cycleMetrics = metrics.BeginCleanupCycle();

        if (!await TryAcquireLock(dbContext, stoppingToken))
        {
            logger.LogDebug("Another instance is running retention cleanup, skipping this cycle");
            metrics.RecordLockSkipped();
            return;
        }

        try
        {
            // Signal cleanup starting - throttling will progressively increase over time
            throttleState.BeginCleanup();
            try
            {
                var totalDeletedMessages = await CleanProcessedMessages(dbContext, cutoff, stoppingToken);
                var totalDeletedSnapshots = await CleanSagaSnapshots(dbContext, cutoff, stoppingToken);

                cycleMetrics.Complete();

                logger.LogInformation("Retention cleanup removed {Messages} messages and {Snapshots} saga snapshots in {Elapsed}",
                    totalDeletedMessages, totalDeletedSnapshots, stopwatch.Elapsed.ToString(@"hh\:mm\:ss"));
            }
            finally
            {
                // Always restore full ingestion capacity when done
                throttleState.EndCleanup();
            }
        }
        finally
        {
            await ReleaseLock(dbContext, stoppingToken);
        }
    }

    async Task<int> CleanProcessedMessages(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken stoppingToken)
    {
        var totalDeleted = 0;
        int deleted;

        do
        {
            using var batchMetrics = metrics.BeginBatch(EntityTypes.Message);

            var strategy = dbContext.Database.CreateExecutionStrategy();
            deleted = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

                // Get BatchIds where ALL messages are expired (using MAX(ProcessedAt))
                // This ensures we only delete complete batches, not partial batches
                var expiredBatchIds = await dbContext.ProcessedMessages
                    .GroupBy(m => m.BatchId)
                    .Select(g => new { BatchId = g.Key, LatestProcessedAt = g.Max(m => m.ProcessedAt) })
                    .Where(b => b.LatestProcessedAt < cutoff)
                    .Select(b => b.BatchId)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                if (expiredBatchIds.Count == 0)
                {
                    return 0;
                }

                // Delete body folders first (non-transactional, fire-and-forget)
                await bodyPersistence.DeleteBatches(expiredBatchIds, stoppingToken);

                // Delete database records for these batches
                var batchDeleted = await dbContext.ProcessedMessages
                    .Where(m => expiredBatchIds.Contains(m.BatchId))
                    .ExecuteDeleteAsync(stoppingToken);

                await transaction.CommitAsync(stoppingToken);
                return batchDeleted;
            });

            batchMetrics.RecordDeleted(deleted);
            totalDeleted += deleted;

            // Delay between batches to reduce contention with ingestion
            if (deleted > 0)
            {
                await Task.Delay(settings.RetentionCleanupBatchDelay, stoppingToken);
            }
        } while (deleted > 0);

        return totalDeleted;
    }

    async Task<int> CleanSagaSnapshots(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken stoppingToken)
    {
        var totalDeleted = 0;
        int deleted;

        do
        {
            using var batchMetrics = metrics.BeginBatch(EntityTypes.SagaSnapshot);

            var strategy = dbContext.Database.CreateExecutionStrategy();
            deleted = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

                // Get BatchIds where all snapshots are expired
                var expiredBatchIds = await dbContext.SagaSnapshots
                    .GroupBy(s => s.BatchId)
                    .Select(g => new { BatchId = g.Key, LatestProcessedAt = g.Max(s => s.ProcessedAt) })
                    .Where(b => b.LatestProcessedAt < cutoff)
                    .Select(b => b.BatchId)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                if (expiredBatchIds.Count == 0)
                {
                    return 0;
                }

                metrics.RecordBatchesDeleted(expiredBatchIds.Count);

                var batchDeleted = await dbContext.SagaSnapshots
                    .Where(s => expiredBatchIds.Contains(s.BatchId))
                    .ExecuteDeleteAsync(stoppingToken);

                await transaction.CommitAsync(stoppingToken);
                return batchDeleted;
            });

            batchMetrics.RecordDeleted(deleted);
            totalDeleted += deleted;

            // Delay between batches to reduce contention with ingestion
            if (deleted > 0)
            {
                await Task.Delay(settings.RetentionCleanupBatchDelay, stoppingToken);
            }
        } while (deleted > 0);

        return totalDeleted;
    }

    protected abstract Task<bool> TryAcquireLock(AuditDbContextBase dbContext, CancellationToken stoppingToken);
    protected abstract Task ReleaseLock(AuditDbContextBase dbContext, CancellationToken stoppingToken);
}
