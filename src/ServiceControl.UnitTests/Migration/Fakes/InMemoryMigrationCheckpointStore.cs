#nullable enable
namespace ServiceControl.UnitTests.Migration.Fakes;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

public sealed class InMemoryMigrationCheckpointStore : IMigrationCheckpointStore
{
    readonly ConcurrentDictionary<string, MigrationCheckpoint> checkpoints = new();

    public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MigrationCheckpoint>>(checkpoints.Values.ToArray());

    public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(checkpoints.TryGetValue(categoryId, out var checkpoint) ? checkpoint : null);

    public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        var storedVersion = checkpoints.TryGetValue(checkpoint.CategoryId, out var stored) ? stored.Version : 0;
        if (storedVersion != checkpoint.Version)
        {
            throw new MigrationCheckpointConflictException($"Checkpoint {checkpoint.CategoryId} was saved from version {checkpoint.Version}, but the stored row is at version {storedVersion}.");
        }

        var saved = checkpoint with { Version = checkpoint.Version + 1 };
        checkpoints[checkpoint.CategoryId] = saved;
        return Task.FromResult(saved);
    }
}
