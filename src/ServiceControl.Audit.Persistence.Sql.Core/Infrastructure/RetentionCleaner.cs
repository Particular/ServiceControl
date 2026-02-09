namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using System.Data.Common;
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
    protected const int BatchSize = 1000;

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

        // Use a dedicated connection for the distributed lock, separate from the DbContext.
        // Session-level locks (sp_getapplock / pg_advisory_lock) are tied to the connection.
        // The DbContext's retry execution strategy can drop and reopen its connection on
        // transient errors, which would silently release a lock held on that connection.
        // By holding the lock on a separate connection, the lock remains stable regardless
        // of what happens to the DbContext's connection during batch operations.
        await using var lockConnection = CreateLockConnection();
        await lockConnection.OpenAsync(stoppingToken);

        if (!await TryAcquireLock(lockConnection, stoppingToken))
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
            await ReleaseLock(lockConnection, stoppingToken);
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

                // Select the oldest expired messages by ProcessedAt, limited to BatchSize.
                // This uses the IX_ProcessedMessages_ProcessedAt index for an efficient
                // range scan from the beginning, stopping after BatchSize rows.
                var expiredMessages = await dbContext.ProcessedMessages
                    .Where(m => m.ProcessedAt < cutoff)
                    .Take(BatchSize)
                    .Select(m => new { m.Id, m.BatchId })
                    .ToListAsync(stoppingToken);

                if (expiredMessages.Count == 0)
                {
                    return 0;
                }

                var messageIds = expiredMessages.Select(m => m.Id).ToList();
                var affectedBatchIds = expiredMessages.Select(m => m.BatchId).Distinct().ToList();

                // Delete the selected messages by their primary keys
                var messagesDeleted = await dbContext.ProcessedMessages
                    .Where(m => messageIds.Contains(m.Id))
                    .ExecuteDeleteAsync(stoppingToken);

                // After deleting, find which affected batches now have zero remaining
                // messages. Only those batch folders can be safely removed from disk.
                // This query is cheap: for each of the ~30 batch IDs, it does an index
                // seek on IX_ProcessedMessages_BatchId_ProcessedAt and returns immediately
                // if any row exists.
                var batchIdsStillInUse = await dbContext.ProcessedMessages
                    .Where(m => affectedBatchIds.Contains(m.BatchId))
                    .Select(m => m.BatchId)
                    .Distinct()
                    .ToListAsync(stoppingToken);

                var fullyDeletedBatchIds = affectedBatchIds
                    .Where(id => !batchIdsStillInUse.Contains(id))
                    .ToList();

                if (fullyDeletedBatchIds.Count > 0)
                {
                    await bodyPersistence.DeleteBatches(fullyDeletedBatchIds, stoppingToken);
                }

                await transaction.CommitAsync(stoppingToken);
                return messagesDeleted;
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

                var expiredIds = await dbContext.SagaSnapshots
                    .Where(s => s.ProcessedAt < cutoff)
                    .Take(BatchSize)
                    .Select(s => s.Id)
                    .ToListAsync(stoppingToken);

                if (expiredIds.Count == 0)
                {
                    return 0;
                }

                var snapshotsDeleted = await dbContext.SagaSnapshots
                    .Where(s => expiredIds.Contains(s.Id))
                    .ExecuteDeleteAsync(stoppingToken);

                await transaction.CommitAsync(stoppingToken);
                return snapshotsDeleted;
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

    protected abstract DbConnection CreateLockConnection();
    protected abstract Task<bool> TryAcquireLock(DbConnection lockConnection, CancellationToken stoppingToken);
    protected abstract Task ReleaseLock(DbConnection lockConnection, CancellationToken stoppingToken);
}
