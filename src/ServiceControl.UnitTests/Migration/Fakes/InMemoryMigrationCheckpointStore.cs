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

    public Task Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        checkpoints[checkpoint.CategoryId] = checkpoint;
        return Task.CompletedTask;
    }
}
