namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using System.Diagnostics;
using Abstractions;
using DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class RetentionCleaner(
    ILogger<RetentionCleaner> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    AuditSqlPersisterSettings settings,
    IBodyStoragePersistence bodyPersistence) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // NO-OPS per requirements
        logger.LogInformation("Starting {ServiceName}", nameof(RetentionCleaner));

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            using PeriodicTimer timer = new(TimeSpan.FromMinutes(5), timeProvider);

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
        var stopwatch = Stopwatch.StartNew();
        const int batchSize = 250;

        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContextBase>();

        var cutoff = timeProvider.GetUtcNow().UtcDateTime - settings.AuditRetentionPeriod;

        var totalDeletedMessages = 0;
        int deleted;

        do
        {
            // Get a batch of IDs to delete so we can clean up body files
            var messageIdsToDelete = await dbContext.ProcessedMessages
                .Where(m => m.ProcessedAt < cutoff)
                .OrderBy(m => m.ProcessedAt)
                .Select(m => m.UniqueMessageId)
                .Take(batchSize)
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

            totalDeletedMessages += deleted;
        } while (deleted == batchSize);

        var totalDeletedSnapshots = 0;

        do
        {
            var snapshotIdsToDelete = await dbContext.SagaSnapshots
                .Where(s => s.ProcessedAt < cutoff)
                .OrderBy(s => s.ProcessedAt)
                .Select(s => s.Id)
                .Take(batchSize)
                .ToListAsync(stoppingToken);

            if (snapshotIdsToDelete.Count == 0)
            {
                break;
            }

            deleted = await dbContext.SagaSnapshots
                .Where(s => snapshotIdsToDelete.Contains(s.Id))
                .ExecuteDeleteAsync(stoppingToken);

            totalDeletedSnapshots += deleted;
        } while (deleted == batchSize);

        logger.LogInformation("Retention cleanup removed {Messages} messages and {Snapshots} saga snapshots in {Elapsed}", totalDeletedMessages, totalDeletedSnapshots, stopwatch.Elapsed.ToString(@"hh\:mm\:ss"));
    }
}
