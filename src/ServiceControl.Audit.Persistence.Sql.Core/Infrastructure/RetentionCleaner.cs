namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using Abstractions;
using DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class RetentionCleaner(
    ILogger<RetentionCleaner> logger,
    TimeProvider timeProvider,
    AuditDbContextBase dbContext,
    AuditSqlPersisterSettings settings) : BackgroundService
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
        var cutoff = timeProvider.GetUtcNow().DateTime - settings.AuditRetentionPeriod;

        var deletedMessages = await dbContext.ProcessedMessages
            .Where(m => m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(stoppingToken);

        var deletedSnapshots = await dbContext.SagaSnapshots
            .Where(s => s.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(stoppingToken);

        logger.LogInformation("Retention cleanup removed {Messages} messages and {Snapshots} saga snapshots", deletedMessages, deletedSnapshots);
    }
}
