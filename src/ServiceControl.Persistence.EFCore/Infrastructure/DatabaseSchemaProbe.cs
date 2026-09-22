namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.DbContexts;

class DatabaseSchemaProbe(ServiceControlDbContext dbContext) : IDatabaseSchemaProbe
{
    public async Task EnsureCurrent(CancellationToken cancellationToken = default)
    {
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pending.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The database is missing {pending.Length} migration(s) this version needs, starting with '{pending[0]}'. "
            + "Run setup on the instance that owns this database, then start this host again. Workers never migrate a database.");
    }
}
