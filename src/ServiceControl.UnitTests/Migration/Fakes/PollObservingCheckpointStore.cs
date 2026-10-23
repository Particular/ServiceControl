#nullable enable
namespace ServiceControl.UnitTests.Migration.Fakes;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

// Records when each poll read the rows, so a test can wait for a poll that saw the state it is judging.
// Only the stall watchdog calls ReadAll, so every ReadAll is one of its polls.
public sealed class PollObservingCheckpointStore(TimeProvider clock) : IMigrationCheckpointStore
{
    readonly InMemoryMigrationCheckpointStore inner = new();
    readonly SemaphoreSlim polled = new(0);
    readonly ConcurrentQueue<DateTime> polledAt = new();
    Exception? readAllFailure;
    int readAllFailuresLeft;

    public void FailReadAll(int times, Exception failure)
    {
        readAllFailuresLeft = times;
        readAllFailure = failure;
    }

    /// <summary>
    /// Waits until a poll has read the rows at the clock's current time, and fails the test if none does within ten
    /// seconds. Polls at earlier times are passed over.
    /// </summary>
    public async Task WaitForAPollAtTheCurrentTime()
    {
        var now = clock.GetUtcNow().UtcDateTime;

        while (true)
        {
            Assert.That(await polled.WaitAsync(TimeSpan.FromSeconds(10)), Is.True, $"no poll read the checkpoints at {now:O}; a watch that stopped polling never reaches one");

            if (polledAt.TryDequeue(out var at) && at == now)
            {
                return;
            }
        }
    }

    public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default)
    {
        polledAt.Enqueue(clock.GetUtcNow().UtcDateTime);
        polled.Release();

        if (readAllFailuresLeft > 0)
        {
            readAllFailuresLeft--;
            return Task.FromException<IReadOnlyList<MigrationCheckpoint>>(readAllFailure!);
        }

        return inner.ReadAll(cancellationToken);
    }

    public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) => inner.Read(categoryId, cancellationToken);

    public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default) => inner.Upsert(checkpoint, cancellationToken);
}
