namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Infrastructure.StartupChecks;
using ServiceControl.Persistence.EFCore.DbContexts;

sealed class PendingMigrationsStartupCheck(ServiceControlDbContext dbContext) : IStartupCheck
{
    public async Task Check(CancellationToken cancellationToken = default)
    {
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

        if (pending.Count > 0)
        {
            throw new InvalidOperationException($"The database has pending migrations. Run the instance with --setup to apply them.");
        }
    }
}
