namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.DataMigration;

// Every method opens its own scope and context through DataStoreBase, so none of them can join a
// caller's transaction. Inside one, call ServiceControlDbContext.UpsertCheckpoint instead.
public class EFMigrationCheckpointStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IMigrationCheckpointStore
{
    public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var entities = await dbContext.MigrationCheckpoints.AsNoTracking().ToListAsync(token);
            return (IReadOnlyList<MigrationCheckpoint>)[.. entities.Select(entity => entity.ToCheckpoint())];
        }, cancellationToken);

    public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var entity = await dbContext.MigrationCheckpoints.AsNoTracking().SingleOrDefaultAsync(e => e.CategoryId == categoryId, token);
            return entity?.ToCheckpoint();
        }, cancellationToken);

    public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => dbContext.UpsertCheckpoint(checkpoint, token), cancellationToken);
}