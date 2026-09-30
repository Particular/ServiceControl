namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;

static class UpsertExtensions
{
    public static async Task UpsertAsync<TEntity>(this AuditDbContext dbContext, object key, Func<TEntity> create, Action<TEntity> update, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var entity = await dbContext.FindAsync<TEntity>([key], cancellationToken);
        if (entity is null)
        {
            entity = create();
            try
            {
                dbContext.Add(entity);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException e) when (dbContext.IsDuplicateKeyException(e))
            {
                await dbContext.Entry(entity).ReloadAsync(cancellationToken);
            }
        }

        update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
