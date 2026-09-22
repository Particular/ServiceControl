namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Npgsql;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Infrastructure;

// A session advisory lock on a dedicated, unpooled connection. Unpooled because a session lock
// outlives a pooled connection's return to the pool, and an unlock that failed would then leave the
// lock held by whichever consumer picks that connection up next.
class PostgreSqlRetentionLock(EFPersisterSettings settings) : IRetentionLock
{
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

            return acquired ? new Handle(connection, resource) : null;
        }
        finally
        {
            if (!acquired)
            {
                await connection.DisposeAsync();
            }
        }
    }

    sealed class Handle(NpgsqlConnection connection, string resource) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_advisory_unlock(hashtext(@resource))";
                command.Parameters.AddWithValue("resource", resource);

                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
