namespace ServiceControl.Persistence.EFCore.SqlServer;

using System.Data;
using Microsoft.Data.SqlClient;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Infrastructure;

// A session application lock on a dedicated, unpooled connection. Unpooled because a session lock
// outlives a pooled connection's return to the pool, and a release that failed would then leave the
// lock held by whichever consumer picks that connection up next.
class SqlServerRetentionLock(EFPersisterSettings settings) : IRetentionLock
{
    readonly string connectionString = new SqlConnectionStringBuilder(settings.ConnectionString) { Pooling = false }.ConnectionString;
    readonly string resource = RetentionLock.ResourceName(settings.Schema);

    public async Task<IAsyncDisposable?> TryAcquire(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(connectionString);
        var acquired = false;

        try
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "sp_getapplock";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Resource", resource);
            command.Parameters.AddWithValue("@LockMode", "Exclusive");
            command.Parameters.AddWithValue("@LockOwner", "Session");
            command.Parameters.AddWithValue("@LockTimeout", 0);
            var result = command.Parameters.Add("@Result", SqlDbType.Int);
            result.Direction = ParameterDirection.ReturnValue;

            await command.ExecuteNonQueryAsync(cancellationToken);

            acquired = result.Value is int status && status >= 0;

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

    sealed class Handle(SqlConnection connection, string resource) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "sp_releaseapplock";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@Resource", resource);
                command.Parameters.AddWithValue("@LockOwner", "Session");

                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
