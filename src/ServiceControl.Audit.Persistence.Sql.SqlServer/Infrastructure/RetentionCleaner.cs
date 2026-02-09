namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Infrastructure;

using System.Data.Common;
using Core.Abstractions;
using Core.DbContexts;
using Core.Infrastructure;
using Microsoft.Data.SqlClient;
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
    string connectionString = settings.ConnectionString;

    protected override DbConnection CreateLockConnection() => new SqlConnection(connectionString);

    protected override async Task<bool> TryAcquireLock(DbConnection lockConnection, CancellationToken stoppingToken)
    {
        // Use SQL Server's sp_getapplock with session-level ownership so the lock persists
        // across multiple transactions within the same connection.
        // LockTimeout = 0 means return immediately if lock cannot be acquired
        // Returns >= 0 on success, < 0 on failure
        await using var command = lockConnection.CreateCommand();
        command.CommandText = @"
            DECLARE @lockResult INT;
            EXEC @lockResult = sp_getapplock
                @Resource = 'retention_cleaner',
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = 0;
            SELECT @lockResult;
        ";

        var result = await command.ExecuteScalarAsync(stoppingToken);
        return result is int lockResult && lockResult >= 0;
    }

    protected override async Task ReleaseLock(DbConnection lockConnection, CancellationToken stoppingToken)
    {
        await using var command = lockConnection.CreateCommand();
        command.CommandText = @"
            EXEC sp_releaseapplock
                @Resource = 'retention_cleaner',
                @LockOwner = 'Session';
        ";

        await command.ExecuteNonQueryAsync(stoppingToken);
    }

    protected override async Task<int> DeleteExpiredMessages(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken stoppingToken)
    {
        // Single server-side statement: DELETE TOP avoids round-tripping IDs as parameters
        // and lets SQL Server optimize lock acquisition for the batch.
        return await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE TOP({0}) FROM [ProcessedMessages] WHERE [ProcessedAt] < {1}",
            [BatchSize, cutoff],
            stoppingToken);
    }

    protected override async Task<int> DeleteExpiredSagaSnapshots(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken stoppingToken)
    {
        return await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE TOP({0}) FROM [SagaSnapshots] WHERE [ProcessedAt] < {1}",
            [BatchSize, cutoff],
            stoppingToken);
    }
}
