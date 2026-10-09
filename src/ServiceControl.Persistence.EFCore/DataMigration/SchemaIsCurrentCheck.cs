namespace ServiceControl.Persistence.EFCore.DataMigration;

using Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Refuses a copy into a database whose schema is behind this build. The writers insert into tables by name, so
/// a missing schema migration turns into a failure part way through the first category instead of a refusal.
/// </summary>
sealed class SchemaIsCurrentCheck(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IMigrationStartupCheck
{
    public string Name => "the target schema is current";

    public Task Run(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            string[] pending = [.. await dbContext.Database.GetPendingMigrationsAsync(token)];

            if (pending.Length > 0)
            {
                throw new Exception($"The database is missing {pending.Length} schema migration(s): {string.Join(", ", pending)}. Run ServiceControl with --setup before starting a migration.");
            }
        }, cancellationToken);
}
