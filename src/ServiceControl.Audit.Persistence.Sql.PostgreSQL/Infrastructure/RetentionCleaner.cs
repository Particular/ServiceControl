namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Infrastructure;

using Core.Abstractions;
using Core.DbContexts;
using Core.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    protected override async Task<bool> TryAcquireLock(AuditDbContextBase dbContext, CancellationToken stoppingToken)
    {
        // Use PostgreSQL's session-level advisory lock so the lock persists
        // across multiple transactions within the same connection.
        // pg_try_advisory_lock returns true if lock acquired, false otherwise
        var sql = "SELECT pg_try_advisory_lock(hashtext('retention_cleaner'))";

        // AsAsyncEnumerable() is required because SqlQueryRaw may return non-composable SQL
        // that cannot have additional operators (like FirstOrDefault's TOP 1) composed on top of it
        var result = await dbContext.Database.SqlQueryRaw<bool>(sql).AsAsyncEnumerable().FirstOrDefaultAsync(stoppingToken);
        return result;
    }

    protected override async Task ReleaseLock(AuditDbContextBase dbContext, CancellationToken stoppingToken)
    {
        var sql = "SELECT pg_advisory_unlock(hashtext('retention_cleaner'))";
        await dbContext.Database.ExecuteSqlRawAsync(sql, stoppingToken);
    }
}
