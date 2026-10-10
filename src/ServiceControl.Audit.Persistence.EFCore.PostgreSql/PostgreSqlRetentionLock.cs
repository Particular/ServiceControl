namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.Extensions.Logging;
using Npgsql;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class PostgreSqlRetentionLock(EFPersisterSettings settings, ILogger<PostgreSqlRetentionLock> logger) : IRetentionLock
{
    // Unpooled, because a session lock would stay with a pooled connection after it returns to the pool.
    readonly string connectionString = new NpgsqlConnectionStringBuilder(settings.ConnectionString) { Pooling = false }.ConnectionString;
    readonly string resource = RetentionLock.ResourceName(settings.Schema);

    public async Task<IAsyncDisposable?> TryAcquire(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(connectionString);
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
