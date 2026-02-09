namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Infrastructure;

using System.Data.Common;
using Core.Abstractions;
using Core.DbContexts;
using Core.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

class RetentionCleaner(
    ILogger<RetentionCleaner> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    AuditSqlPersisterSettings settings,
    IBodyStoragePersistence bodyPersistence,
    RetentionMetrics metrics,
    IngestionThrottleState throttleState)
    : Core.Infrastructure.RetentionCleaner(logger, timeProvider, serviceScopeFactory, settings, bodyPersistence, metrics, throttleState)
{
    string connectionString = settings.ConnectionString;

    protected override DbConnection CreateLockConnection() => new NpgsqlConnection(connectionString);

    protected override async Task<bool> TryAcquireLock(DbConnection lockConnection, CancellationToken stoppingToken)
    {
        // Use PostgreSQL's session-level advisory lock so the lock persists
        // across multiple transactions within the same connection.
        // pg_try_advisory_lock returns true if lock acquired, false otherwise
        await using var command = lockConnection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(hashtext('retention_cleaner'))";

        var result = await command.ExecuteScalarAsync(stoppingToken);
        return result is true;
    }

    protected override async Task ReleaseLock(DbConnection lockConnection, CancellationToken stoppingToken)
    {
        await using var command = lockConnection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(hashtext('retention_cleaner'))";

        await command.ExecuteNonQueryAsync(stoppingToken);
    }

    protected override async Task<int> DeleteExpiredMessages(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken stoppingToken)
    {
        // PostgreSQL doesn't support DELETE TOP(N), so use a subquery with ctid
        return await dbContext.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM "ProcessedMessages"
            WHERE ctid IN (
                SELECT ctid FROM "ProcessedMessages"
                WHERE "ProcessedAt" < {0}
                LIMIT {1}
            )
            """,
            [cutoff, BatchSize],
            stoppingToken);
    }

    protected override async Task<int> DeleteExpiredSagaSnapshots(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken stoppingToken)
    {
        return await dbContext.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM "SagaSnapshots"
            WHERE ctid IN (
                SELECT ctid FROM "SagaSnapshots"
                WHERE "ProcessedAt" < {0}
                LIMIT {1}
            )
            """,
            [cutoff, BatchSize],
            stoppingToken);
    }
}
