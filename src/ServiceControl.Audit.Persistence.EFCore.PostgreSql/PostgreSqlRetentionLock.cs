namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class PostgreSqlRetentionLock(
    EFPersisterSettings settings,
    ILogger<PostgreSqlRetentionLock> logger,
    [FromKeyedServices(PostgreSqlRetentionLock.TokenDataSourceKey)] NpgsqlDataSource? tokenDataSource = null) : IRetentionLock
{
    public const string TokenDataSourceKey = nameof(PostgreSqlRetentionLock);

    readonly string connectionString = Unpooled(settings.ConnectionString);
    readonly string resource = RetentionLock.ResourceName(settings.Schema);

    // Unpooled, because a session lock would stay with a pooled connection after it returns to the pool.
    public static string Unpooled(string connectionString) => new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;

    public async Task<IAsyncDisposable?> TryAcquire(CancellationToken cancellationToken = default)
    {
        var connection = tokenDataSource?.CreateConnection() ?? new NpgsqlConnection(connectionString);
        var acquired = false;
        try
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(hashtext(@resource))";
            command.Parameters.AddWithValue("resource", resource);

            acquired = await command.ExecuteScalarAsync(cancellationToken) is true;
            return acquired ? new Handle(connection, resource, logger) : null;
        }
        finally
        {
            if (!acquired)
            {
                await connection.DisposeAsync();
            }
        }
    }

    sealed class Handle(NpgsqlConnection connection, string resource, ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_advisory_unlock(hashtext(@resource))";
                command.Parameters.AddWithValue("resource", resource);

                if (await command.ExecuteScalarAsync() is not true)
                {
                    logger.LogWarning("The audit retention lock was not held by this connection when releasing it. Connect to PostgreSQL directly or through a session mode pooler, or retention will stop running");
                }
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
