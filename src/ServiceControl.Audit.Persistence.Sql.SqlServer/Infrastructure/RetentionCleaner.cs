namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Infrastructure;

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
    IBodyStoragePersistence bodyPersistence)
    : Core.Infrastructure.RetentionCleaner(logger, timeProvider, serviceScopeFactory, settings, bodyPersistence)
{
    protected override async Task<bool> TryAcquireLock(AuditDbContextBase dbContext, CancellationToken stoppingToken)
    {
        // Use SQL Server's sp_getapplock for distributed locking
        // LockTimeout = 0 means return immediately if lock cannot be acquired
        // Returns >= 0 on success, < 0 on failure
        var sql = @"
            DECLARE @lockResult INT;
            EXEC @lockResult = sp_getapplock
                @Resource = 'retention_cleaner',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 0;
            SELECT @lockResult;
        ";

        var result = await dbContext.Database.SqlQueryRaw<int>(sql).FirstOrDefaultAsync(stoppingToken);
        return result >= 0;
    }
}
