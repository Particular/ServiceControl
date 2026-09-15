#nullable enable
namespace ServiceControl.UnitTests.Migration.Fakes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

public sealed class InMemoryMigrationSource : IMigrationSource
{
    readonly Dictionary<string, List<MigrationRow>> rowsByCategory = [];
    readonly Dictionary<string, Queue<Func<MigrationBody?>>> bodyAttempts = [];
    readonly Dictionary<string, MigrationBody?> bodies = [];

    public MigrationSourceDescription Description { get; set; } = new("in-memory", [new MigrationSourceFact("Store", "in memory")]);

    public void Seed(string categoryId, params MigrationRow[] rows) => rowsByCategory[categoryId] = [.. rows];

    public void SetBody(string sourceId, MigrationBody? body) => bodies[sourceId] = body;

    public void QueueBodyAttempt(string sourceId, Func<MigrationBody?> attempt)
    {
        if (!bodyAttempts.TryGetValue(sourceId, out var queue))
        {
            bodyAttempts[sourceId] = queue = new Queue<Func<MigrationBody?>>();
        }
        queue.Enqueue(attempt);
    }

    public Task Open(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<MigrationSourceDescription> Describe(CancellationToken cancellationToken = default) => Task.FromResult(Description);

    public Task<IReadOnlyList<MigrationSourceInventoryEntry>> Inventory(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MigrationSourceInventoryEntry>>(
            [.. rowsByCategory.Select(pair => new MigrationSourceInventoryEntry("memory", pair.Key, pair.Value.Count))]);

    public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) =>
        Task.FromResult((long)(rowsByCategory.TryGetValue(category.Id, out var rows) ? rows.Count : 0));

    public async IAsyncEnumerable<MigrationBatch> Read(
        MigrationCategory category,
        string? resumeAfter,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rows = rowsByCategory.GetValueOrDefault(category.Id) ?? [];
        var startIndex = 0;

        if (resumeAfter is not null)
        {
            var cursorIndex = rows.FindIndex(r => r.SourceId == resumeAfter);
            if (cursorIndex < 0)
            {
                throw new InvalidOperationException($"Category {category.Id} was asked to resume after {resumeAfter}, a cursor this source never issued");
            }
            startIndex = cursorIndex + 1;
        }

        for (var i = startIndex; i < rows.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slice = rows.Skip(i).Take(batchSize).ToArray();
            yield return new MigrationBatch(slice, slice[^1].SourceId);
            await Task.Yield();
        }
    }

    public async Task<MigrationBody?> ReadBody(MigrationCategory category, string sourceId, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        if (bodyAttempts.TryGetValue(sourceId, out var queue) && queue.Count > 0)
        {
            return queue.Dequeue()();
        }

        return bodies.GetValueOrDefault(sourceId);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
