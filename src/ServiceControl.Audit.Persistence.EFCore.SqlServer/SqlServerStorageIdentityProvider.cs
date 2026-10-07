namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Audit.Persistence;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;

// The query and the default schema must stay identical to the primary instance's provider, or the
// two sides hash different values for one database and DatabaseSharing never matches.
class SqlServerStorageIdentityProvider(EFPersisterSettings settings, IServiceScopeFactory scopeFactory, ILogger<SqlServerStorageIdentityProvider> logger) : IStorageIdentityProvider
{
    public async ValueTask<StorageIdentity?> GetIdentity(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            // SCHEMA_NAME() is where unqualified tables go when no schema is configured.
            command.CommandText = "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(256)), DB_NAME(), SCHEMA_NAME()";
            command.CommandTimeout = ProbeTimeoutSeconds;

            await dbContext.Database.OpenConnectionAsync(cancellationToken);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1))
            {
                return null;
            }

            return new StorageIdentity("SQLServer", reader.GetString(0), reader.GetString(1), settings.Schema ?? (reader.IsDBNull(2) ? null : reader.GetString(2)));
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
}
