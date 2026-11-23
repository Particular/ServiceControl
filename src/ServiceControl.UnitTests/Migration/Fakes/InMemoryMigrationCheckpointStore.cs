#nullable enable
namespace ServiceControl.UnitTests.Migration.Fakes;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

public sealed class InMemoryMigrationCheckpointStore : IMigrationCheckpointStore
{
    readonly ConcurrentDictionary<string, MigrationCheckpoint> checkpoints = new();
    int readAllCount;

    /// <summary>
    /// Thrown by every read while it is set.
    /// </summary>
    public Exception? ReadFailure { get; set; }

    /// <summary>
    /// How many times <see cref="ReadAll" /> has been called, failed calls included. It counts after the read, so a test waiting on it sees the rows as they were then.
    /// </summary>
    public int ReadAllCount => readAllCount;

    public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default)
    {
        if (ReadFailure is { } failure)
        {
            Interlocked.Increment(ref readAllCount);
            return Task.FromException<IReadOnlyList<MigrationCheckpoint>>(failure);
        }

        var read = checkpoints.Values.ToArray();
        Interlocked.Increment(ref readAllCount);
        return Task.FromResult<IReadOnlyList<MigrationCheckpoint>>(read);
    }

    public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) =>
        ReadFailure is { } failure
            ? Task.FromException<MigrationCheckpoint?>(failure)
            : Task.FromResult(checkpoints.TryGetValue(categoryId, out var checkpoint) ? checkpoint : null);

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
