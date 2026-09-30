namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.DbContexts;

class SqlServerStorageIdentityProvider(SqlServerPersisterSettings settings, IServiceScopeFactory scopeFactory, ILogger<SqlServerStorageIdentityProvider> logger) : IStorageIdentityProvider
{
    public async ValueTask<StorageIdentity?> GetIdentity(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(256)), DB_NAME()";
            command.CommandTimeout = ProbeTimeoutSeconds;

            await dbContext.Database.OpenConnectionAsync(cancellationToken);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1))
            {
                return null;
            }

            return new StorageIdentity("SQLServer", reader.GetString(0), reader.GetString(1), settings.Schema ?? DefaultSchema);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Could not read the SQL Server storage identity");

            return null;
        }
    }

    const int ProbeTimeoutSeconds = 5;
    const string DefaultSchema = "dbo";
}
