namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

sealed class AuditRetention(
    IServiceScopeFactory scopeFactory,
    IAuditPartitionManager partitions,
    IRetentionLock retentionLock,
    EFPersisterSettings settings,
    TimeProvider timeProvider,
    ILogger<AuditRetention> logger) : BackgroundService
{
    // Stays under the 5,000 locks at which SQL Server escalates a delete to a table lock.
    const int BatchSize = 4_000;
    static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    static readonly TimeSpan BatchPause = TimeSpan.FromSeconds(1);

    // Before ingestion starts, which cannot insert into an hour that has no partition.
    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var hour = AuditHours.Truncate(timeProvider.GetUtcNow().UtcDateTime);
            await partitions.EnsurePartitions(dbContext, hour.AddHours(-1), hour + AuditHours.Lookahead, cancellationToken);
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
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
#pragma warning disable PS0019 // Filtered on the token alone because SqlClient reports a cancelled command as a SqlException.
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Error during audit retention sweep");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping audit retention sweep");
        }
#pragma warning restore PS0019
    }

    internal Task SweepNow(CancellationToken cancellationToken = default) => Sweep(pace: false, cancellationToken);

    async Task Sweep(bool pace, CancellationToken cancellationToken)
    {
        await using var handle = await retentionLock.TryAcquire(cancellationToken);
        if (handle is null)
        {
            logger.LogWarning("Skipping the audit retention sweep because another instance holds the retention lock. Only one audit instance should own a database.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hour = AuditHours.Truncate(now);
        await partitions.EnsurePartitions(dbContext, hour, hour + AuditHours.Lookahead, cancellationToken);

        var keepFrom = AuditHours.Truncate(now - settings.AuditRetentionPeriod);

        foreach (var expired in await partitions.ListExpired(dbContext, keepFrom, cancellationToken))
        {
            DropResult drop;
            do
            {
                drop = await partitions.DropExpired(dbContext, expired, BatchSize, cancellationToken);

                if (drop.Deferred)
                {
                    logger.LogInformation("Left the audit rows from {Start:u} for the next sweep because the audit tables were too busy to drop them", expired);
                    return;
                }

                if (pace && !drop.Completed)
                {
                    await Task.Delay(BatchPause, timeProvider, cancellationToken);
                }
            } while (!drop.Completed);

            logger.LogDebug("Removed the expired audit rows from {Start:u}", expired);
        }
    }
}
