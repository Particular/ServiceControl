namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.DbContexts;

class PostgreSqlStorageIdentityProvider(PostgreSqlPersisterSettings settings, IServiceScopeFactory scopeFactory, ILogger<PostgreSqlStorageIdentityProvider> logger) : IStorageIdentityProvider
{
    public async ValueTask<StorageIdentity?> GetIdentity(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            // The cluster's initialisation identifier rather than an address: every connection to
            // the same cluster sees the same value however the host is spelled or proxied.
            // current_schema() is where unqualified tables go when no schema is configured.
            command.CommandText = "SELECT system_identifier::text, current_database(), current_schema() FROM pg_control_system()";
            command.CommandTimeout = ProbeTimeoutSeconds;

            await dbContext.Database.OpenConnectionAsync(cancellationToken);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1))
            {
                return null;
            }

            return new StorageIdentity("PostgreSQL", reader.GetString(0), reader.GetString(1), settings.Schema ?? (reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Could not read the PostgreSQL storage identity");

            return null;
        }
    }

    const int ProbeTimeoutSeconds = 5;
}
