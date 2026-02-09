namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using System.Data.Common;
using System.Diagnostics;
using Abstractions;
using DbContexts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public abstract class RetentionCleaner(
    ILogger logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    AuditSqlPersisterSettings settings,
    IBodyStoragePersistence bodyPersistence,
    IPartitionManager partitionManager,
    RetentionMetrics metrics) : BackgroundService
{
    const int DaysAhead = 3;

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
        // Use a dedicated connection for the distributed lock so it is not affected
        // by connection drops or resets on the main DbContext during cleanup operations
        await using var lockConnection = CreateConnection();
        await lockConnection.OpenAsync(stoppingToken);

        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContextBase>();

        var stopwatch = Stopwatch.StartNew();
        var cutoff = timeProvider.GetUtcNow().UtcDateTime - settings.AuditRetentionPeriod;
        var today = timeProvider.GetUtcNow().UtcDateTime.Date;

        using var cycleMetrics = metrics.BeginCleanupCycle();

        if (!await TryAcquireLock(lockConnection, stoppingToken))
        {
            logger.LogDebug("Another instance is running retention cleanup, skipping this cycle");
            metrics.RecordLockSkipped();
            return;
        }

        try
        {
            // Ensure partitions exist for upcoming days
            await partitionManager.EnsurePartitionsExist(dbContext, today, DaysAhead, stoppingToken);

            // Find and drop expired partitions
            var expiredDates = await partitionManager.GetExpiredPartitionDates(dbContext, cutoff, stoppingToken);

            foreach (var date in expiredDates)
            {
                // Delete body storage for this date first
                await bodyPersistence.DeleteBodiesForDate(date, stoppingToken);

                // Drop the database partition
                await partitionManager.DropPartition(dbContext, date, stoppingToken);

                metrics.RecordPartitionDropped();

                logger.LogInformation("Dropped partition for {Date}", date.ToString("yyyy-MM-dd"));
            }

            cycleMetrics.Complete();

            logger.LogInformation("Retention cleanup dropped {Partitions} partition(s) in {Elapsed}",
                expiredDates.Count, stopwatch.Elapsed.ToString(@"hh\:mm\:ss"));
        }
        finally
        {
            await ReleaseLock(lockConnection, stoppingToken);
        }
    }

    protected abstract DbConnection CreateConnection();
    protected abstract Task<bool> TryAcquireLock(DbConnection lockConnection, CancellationToken stoppingToken);
    protected abstract Task ReleaseLock(DbConnection lockConnection, CancellationToken stoppingToken);
}
