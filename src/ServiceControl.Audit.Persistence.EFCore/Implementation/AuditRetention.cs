namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure.Metrics;

sealed class AuditRetention(
    IServiceScopeFactory scopeFactory,
    IAuditPartitionManager partitions,
    IRetentionLock retentionLock,
    AuditRetentionCustomCheck.State retentionState,
    EFPersisterSettings settings,
    AuditRetentionMetrics metrics,
    TimeProvider timeProvider,
    ILogger<AuditRetention> logger,
    IAuditBodyStoragePersistence? bodyStorage = null) : BackgroundService
{
    // Stays under the 5,000 locks at which SQL Server escalates a delete to a table lock.
    const int BatchSize = 4_000;
    static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    static readonly TimeSpan BatchPause = TimeSpan.FromSeconds(1);
    static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(10);
    const string CommandTimeoutSetting = $"ServiceControl.Audit/{EFPersistenceConfigurationBase.CommandTimeoutKey}";

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
                var attempt = 0;
                while (!await TrySweep(cancellationToken))
                {
                    var delay = RetryDelay(++attempt, Random.Shared.NextDouble());
                    logger.LogWarning("The audit retention sweep failed. Attempt {Attempt} starts in {Delay}", attempt + 1, delay);
                    await Task.Delay(delay, timeProvider, cancellationToken);
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
#pragma warning disable PS0019 // Filtered on the token alone because SqlClient reports a cancelled command as a SqlException.
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping audit retention sweep");
        }
#pragma warning restore PS0019
    }

    async Task<bool> TrySweep(CancellationToken cancellationToken)
    {
        try
        {
            return await Sweep(pace: true, cancellationToken);
        }
#pragma warning disable PS0019 // Filtered on the token alone because SqlClient reports a cancelled command as a SqlException.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Error during audit retention sweep");
            return false;
        }
#pragma warning restore PS0019
    }

    // Exponential back-off with jitter, capped at the sweep interval. jitter is in [0, 1).
    internal static TimeSpan RetryDelay(int attempt, double jitter)
    {
        var exponential = TimeSpan.FromTicks(Math.Min(Interval.Ticks, RetryBaseDelay.Ticks << Math.Min(attempt - 1, 16)));
        return (exponential / 2) + (exponential / 2 * jitter);
    }

    internal Task SweepNow(CancellationToken cancellationToken = default) => Sweep(pace: false, cancellationToken);

    // Returns false when a pass failed.
    async Task<bool> Sweep(bool pace, CancellationToken cancellationToken)
    {
        var passesStarted = false;
        try
        {
            List<string> passFailures = [];
            await using (var handle = await retentionLock.TryAcquire(cancellationToken))
            {
                if (handle is null)
                {
                    logger.LogWarning("Skipping the audit retention sweep because another instance holds the retention lock. Only one audit instance should own a database.");
                    return true;
                }

                var now = timeProvider.GetUtcNow().UtcDateTime;
                await EnsurePartitions(AuditHours.Truncate(now), cancellationToken);

                var keepFrom = AuditHours.Truncate(now - settings.AuditRetentionPeriod);
                passesStarted = true;
                foreach (var entity in Entities)
                {
                    if (await RunPass(entity, keepFrom, pace, cancellationToken) is { } failure)
                    {
                        passFailures.Add(failure);
                    }
                }
            }

            if (passFailures.Count > 0)
            {
                retentionState.SweepFailed(string.Join("; ", passFailures));
                return false;
            }

            retentionState.SweepSucceeded();
            return true;
        }
#pragma warning disable PS0019 // Filtered on the token alone because SqlClient reports a cancelled command as a SqlException.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // No pass ran, so each one is recorded as failed and the gauge still climbs.
            if (!passesStarted)
            {
                foreach (var entity in Entities)
                {
                    metrics.BeginCycle(entity, cancellationToken).Dispose();
                }
            }

            retentionState.SweepFailed(ex.Message);
            throw;
        }
#pragma warning restore PS0019
    }

    async Task EnsurePartitions(DateTime hour, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        await partitions.EnsurePartitions(dbContext, hour, hour + AuditHours.Lookahead, cancellationToken);
    }

    // Each pass is isolated so one failing kind of row does not stop the other from being reclaimed.
    // Returns what went wrong for the custom check to report, or null when the pass succeeded.
    async Task<string?> RunPass(AuditRetentionEntity entity, DateTime keepFrom, bool pace, CancellationToken cancellationToken)
    {
        using var cycle = metrics.BeginCycle(entity, cancellationToken);

        try
        {
            // A busy table keeps every expired row from the hour where it stopped, so the pass fails rather than letting retention stall with no warning.
            if (await RemoveExpired(entity, keepFrom, pace, cancellationToken) is { } deferred)
            {
                return $"{entity}: the table was too busy to drop the rows from {deferred:u}";
            }

            cycle.Complete();
            return null;
        }
#pragma warning disable PS0019 // Filtered on the token alone because SqlClient reports a cancelled command as a SqlException.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (partitions.IsCommandTimeout(ex))
            {
                logger.LogError(ex, "The {RetentionEntity} audit retention pass exceeded the command timeout of {CommandTimeout} seconds. If this continues, increase the {Setting} setting", entity, settings.CommandTimeout, CommandTimeoutSetting);
                return $"{entity}: a command exceeded the timeout of {settings.CommandTimeout} seconds. If this continues, increase the {CommandTimeoutSetting} setting";
            }

            logger.LogError(ex, "Error during the {RetentionEntity} audit retention pass", entity);
            return $"{entity}: {ex.Message}";
        }
#pragma warning restore PS0019
    }

    // Returns the start of the expired rows where it stopped because the table was too busy, or null when it removed every expired row.
    async Task<DateTime?> RemoveExpired(AuditRetentionEntity entity, DateTime keepFrom, bool pace, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        foreach (var expired in await partitions.ListExpired(dbContext, entity, keepFrom, cancellationToken))
        {
            // Bodies go first: a failure here leaves the rows in place, so the next sweep retries the same hour.
            if (bodyStorage is not null)
            {
                await partitions.DeleteBodies(bodyStorage, expired, cancellationToken);
            }

            DropResult drop;
            do
            {
                drop = await partitions.DropExpired(dbContext, entity, expired, BatchSize, cancellationToken);

                if (drop.Deferred)
                {
                    logger.LogInformation("Left the {RetentionEntity} rows from {Start:u} for the next sweep because the table was too busy to drop them", entity, expired);
                    return expired;
                }

                if (pace && !drop.Completed)
                {
                    await Task.Delay(BatchPause, timeProvider, cancellationToken);
                }
            } while (!drop.Completed);

            logger.LogDebug("Removed the expired {RetentionEntity} rows from {Start:u}", entity, expired);
        }

        return null;
    }

    static readonly AuditRetentionEntity[] Entities = Enum.GetValues<AuditRetentionEntity>();
}
