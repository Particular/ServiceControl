namespace ServiceControl.Persistence.EFCore;

using DbContexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.DataMigration;

public static class MigrationCheckpointExtensions
{
    /// <summary>
    /// Saves a migration checkpoint on the caller's own context, so it commits with that context's transaction
    /// and flushes whatever else that context is tracking, and returns it carrying the version the save landed on.
    /// Throws <see cref="MigrationCheckpointConflictException"/> when the stored row no longer holds
    /// <see cref="MigrationCheckpoint.Version"/>.
    /// </summary>
    public static async Task<MigrationCheckpoint> UpsertCheckpoint(this ServiceControlDbContext dbContext, MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.UpsertCheckpointCore(checkpoint, cancellationToken);
        }
        catch (DbUpdateConcurrencyException e)
        {
            throw new MigrationCheckpointConflictException(
                $"Checkpoint {checkpoint.CategoryId} was saved from version {checkpoint.Version}, which the stored row no longer holds. Another writer advanced it, or a retried save is meeting its own committed write after a lost acknowledgement.", e);
        }
    }

    // Deliberately not UpsertAsync: its duplicate-key fallback reloads and saves again, which inside a
    // caller-opened PostgreSQL transaction runs after the failed insert aborted it and fails with 25P02.
    static async Task<MigrationCheckpoint> UpsertCheckpointCore(this ServiceControlDbContext dbContext, MigrationCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var entity = await dbContext.FindAsync<MigrationCheckpointEntity>([checkpoint.CategoryId], cancellationToken: cancellationToken);

        if (entity is null)
        {
            if (checkpoint.Version != 0)
            {
                throw new MigrationCheckpointConflictException($"Checkpoint {checkpoint.CategoryId} was saved from version {checkpoint.Version}, but no row exists for it.");
            }

            var inserted = Insert(checkpoint);
            dbContext.Add(inserted);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (dbContext.IsDuplicateKeyException(e))
            {
                // Another writer created this category's first checkpoint between the read and the insert. Not
                // retried: PostgreSQL has already aborted the caller's transaction, so a retry cannot run.
                throw new MigrationCheckpointConflictException(
                    $"Checkpoint {checkpoint.CategoryId} was saved as a first write, but another writer had already created it.", e);
            }

            return inserted.ToCheckpoint();
        }

        // The expected version goes in the UPDATE's WHERE clause, so a save from an out-of-date copy matches no row.
        dbContext.Entry(entity).Property(e => e.Version).OriginalValue = checkpoint.Version;
        entity.Version = checkpoint.Version + 1;
        Apply(checkpoint, entity);

        await dbContext.SaveChangesAsync(cancellationToken);

        return entity.ToCheckpoint();
    }

    static MigrationCheckpointEntity Insert(MigrationCheckpoint checkpoint)
    {
        var entity = new MigrationCheckpointEntity { CategoryId = checkpoint.CategoryId, Version = 1 };

        Apply(checkpoint, entity);

        return entity;
    }

    // One place both branches copy the row through, so a column added to the checkpoint cannot be written on
    // an update and forgotten on an insert.
    static void Apply(MigrationCheckpoint checkpoint, MigrationCheckpointEntity entity)
    {
        entity.State = checkpoint.State;
        entity.Cursor = checkpoint.Cursor;
        entity.CopiedCount = checkpoint.CopiedCount;
        entity.SkippedCount = checkpoint.SkippedCount;
        entity.SourceTotal = checkpoint.SourceTotal;
        entity.SkipReasons = checkpoint.SkipReasons;
        entity.StartedAt = checkpoint.StartedAt;
        entity.LastProgressAt = checkpoint.LastProgressAt;
        entity.SettledAt = checkpoint.SettledAt;
        entity.LastError = checkpoint.LastError;
        entity.AlreadyPresentCount = checkpoint.AlreadyPresentCount;
    }
}
