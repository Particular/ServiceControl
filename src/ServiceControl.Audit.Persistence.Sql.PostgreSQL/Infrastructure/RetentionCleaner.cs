namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Infrastructure;

using System.Data.Common;
using Core.Abstractions;
using Core.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

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
    protected override DbConnection CreateConnection() => new NpgsqlConnection(settings.ConnectionString);

    protected override async Task<bool> TryAcquireLock(DbConnection lockConnection, CancellationToken stoppingToken)
    {
        await using var cmd = lockConnection.CreateCommand();
        cmd.CommandText = "SELECT pg_try_advisory_lock(hashtext('retention_cleaner'))";

        var result = await cmd.ExecuteScalarAsync(stoppingToken);
        return result is true;
    }

    protected override async Task ReleaseLock(DbConnection lockConnection, CancellationToken stoppingToken)
    {
        await using var cmd = lockConnection.CreateCommand();
        cmd.CommandText = "SELECT pg_advisory_unlock(hashtext('retention_cleaner'))";

        await cmd.ExecuteNonQueryAsync(stoppingToken);
    }
}
