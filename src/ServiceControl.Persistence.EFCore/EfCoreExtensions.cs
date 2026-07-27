namespace ServiceControl.Persistence.EFCore;

using DbContexts;
using Microsoft.EntityFrameworkCore;

public static class EfCoreExtensions
{
    /// <summary>
    /// Inserts a new entity when it does not exist, or updates the existing entity when it does.
    /// </summary>
    /// <param name="context">The EF Core database context used to query and persist the entity.</param>
    /// <param name="keys">The key values used by <see cref="DbContext.FindAsync{TEntity}(object?[], CancellationToken)"/> to locate the entity.</param>
    /// <param name="create">Factory used to create a new entity instance when no matching entity is found.</param>
    /// <param name="update">Update action applied to the resolved entity before saving changes.</param>
    /// <param name="cancellationToken">Token used to cancel database operations.</param>
    /// <typeparam name="TContext">A <see cref="ServiceControlDbContext"/> implementation.</typeparam>
    /// <typeparam name="TEntity">The entity type to insert or update.</typeparam>
    /// <remarks>
    /// This extension method is intended to be a zero dependency utility and has been implemented
    /// in a relatively naive way using standard EF building blocks.
    /// If more performance critical uses for this are required please consider either implementing your
    /// upserts using dialect specific implementations or a library such as https://github.com/artiomchi/FlexLabs.Upsert
    /// </remarks>
    public static async Task UpsertAsync<TContext, TEntity>(this TContext context,
        object?[] keys,
        Func<TEntity> create,
        Action<TEntity> update,
        CancellationToken cancellationToken = default)
        where TContext : ServiceControlDbContext
        where TEntity : class
    {
        var entity = await context.FindAsync<TEntity>(keys, cancellationToken: cancellationToken);
        if (entity == null)
        {
            entity = create();
            try
            {
                context.Add(entity);
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException e) when (context.IsDuplicateKeyException(e))
            {
                //most likely an insert conflict, reload the object and fall through to the update logic
                await context.Entry(entity).ReloadAsync(cancellationToken);
            }
        }

        update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}