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
    RetentionMetrics metrics)
    : Core.Infrastructure.RetentionCleaner(logger, timeProvider, serviceScopeFactory, settings, bodyPersistence, metrics)
{
    protected override async Task<bool> TryAcquireLock(AuditDbContextBase dbContext, CancellationToken stoppingToken)
    {
        // Use PostgreSQL's advisory lock for distributed locking
        // pg_try_advisory_xact_lock returns true if lock acquired, false otherwise
        // The lock is automatically released when the transaction ends
        var sql = "SELECT pg_try_advisory_xact_lock(hashtext('retention_cleaner'))";

        // AsAsyncEnumerable() is required because SqlQueryRaw may return non-composable SQL
        // that cannot have additional operators (like FirstOrDefault's TOP 1) composed on top of it
        var result = await dbContext.Database.SqlQueryRaw<bool>(sql).AsAsyncEnumerable().FirstOrDefaultAsync(stoppingToken);
        return result;
    }
}
