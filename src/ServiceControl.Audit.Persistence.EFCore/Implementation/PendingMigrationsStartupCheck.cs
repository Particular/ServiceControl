namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Infrastructure.StartupChecks;

sealed class PendingMigrationsStartupCheck(AuditDbContext dbContext) : IStartupCheck
{
    public async Task Check(CancellationToken cancellationToken = default)
    {
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

        if (pending.Count > 0)
        {
            throw new InvalidOperationException($"The audit database has pending migrations: {string.Join(", ", pending)}. Run the instance with --setup to apply them.");
        }
    }
}
