namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Infrastructure;

using System.Data.Common;
using Core.Abstractions;
using Core.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

class RetentionCleaner(
    ILogger<RetentionCleaner> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    AuditSqlPersisterSettings settings,
    IBodyStoragePersistence bodyPersistence,
    IPartitionManager partitionManager,
    RetentionMetrics metrics)
    : Core.Infrastructure.RetentionCleaner(logger, timeProvider, serviceScopeFactory, settings, bodyPersistence, partitionManager, metrics)
{
    readonly string connectionString = settings.ConnectionString;

    protected override DbConnection CreateConnection() => new SqlConnection(connectionString);

    protected override async Task<bool> TryAcquireLock(DbConnection lockConnection, CancellationToken stoppingToken)
    {
        await using var cmd = lockConnection.CreateCommand();
        cmd.CommandText = """
            DECLARE @lockResult INT;
            EXEC @lockResult = sp_getapplock
                @Resource = 'retention_cleaner',
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = 0;
            SELECT @lockResult;
            """;

        var result = await cmd.ExecuteScalarAsync(stoppingToken);
        return result is int lockResult && lockResult >= 0;
    }

    protected override async Task ReleaseLock(DbConnection lockConnection, CancellationToken stoppingToken)
    {
        await using var cmd = lockConnection.CreateCommand();
        cmd.CommandText = """
            EXEC sp_releaseapplock
                @Resource = 'retention_cleaner',
                @LockOwner = 'Session';
            """;

        await cmd.ExecuteNonQueryAsync(stoppingToken);
    }
}
