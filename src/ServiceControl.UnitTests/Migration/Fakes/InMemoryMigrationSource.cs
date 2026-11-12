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
    readonly Dictionary<string, (int Times, Exception Failure)> bodyFailures = [];
    readonly Dictionary<string, Exception[]> bodyFailuresInTurn = [];
    readonly Dictionary<string, int> bodyReadAttempts = [];
    readonly Dictionary<string, MigrationBody?> bodies = [];
    // The loader reads several bodies at once, so the attempt counts and the in-flight count are shared between threads.
    readonly Lock bodyReadGate = new();
    TaskCompletionSource? heldBodyReads;
    int bodyReadsInFlight;

    public MigrationSourceDescription Description { get; set; } = new("in-memory", [new MigrationSourceFact("Store", "in memory")]);

    public void Seed(string categoryId, params MigrationRow[] rows) => rowsByCategory[categoryId] = [.. rows];

    public void SetBody(string sourceId, MigrationBody? body) => bodies[sourceId] = body;

    public void FailBodyReads(string sourceId, int times, Exception failure) => bodyFailures[sourceId] = (times, failure);

    /// <summary>
    /// Fails the body reads for this row with one exception per attempt, in order, so a test can tell one attempt's error from another's. An attempt past the end of the list reads the body normally.
    /// </summary>
    public void FailBodyReadsInTurn(string sourceId, params Exception[] failuresInAttemptOrder) => bodyFailuresInTurn[sourceId] = failuresInAttemptOrder;

    /// <summary>
    /// Makes the body read for this row behave like a host shutting down: the token is cancelled and the read throws.
    /// </summary>
    public (string SourceId, CancellationTokenSource Source)? StopOnBodyRead { get; set; }

    public int BodyReadAttempts(string sourceId)
    {
        lock (bodyReadGate)
        {
            return bodyReadAttempts.GetValueOrDefault(sourceId);
        }
    }

    /// <summary>
    /// The most body reads that were open at the same moment since the source was created.
    /// </summary>
    public int PeakBodyReadsInFlight { get; private set; }

    /// <summary>
    /// Makes every body read wait until <see cref="ReleaseBodyReads" /> is called. A waiting read already counts as an attempt and as open.
    /// </summary>
    public void HoldBodyReads() => heldBodyReads = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Lets every held body read carry on, and every later one run without waiting. Call <see cref="HoldBodyReads" /> first.
    /// </summary>
    public void ReleaseBodyReads() => heldBodyReads!.SetResult();

    /// <summary>
    /// Completes once at least this many body reads are open at the same moment.
    /// </summary>
    public async Task BodyReadsInFlight(int count, CancellationToken cancellationToken = default)
    {
        while (Volatile.Read(ref bodyReadsInFlight) < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        }
    }

    public Task Open(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IReadOnlyList<IMigrationStartupCheck> ContributedChecks() => [];

    public Task<MigrationSourceDescription> Describe(CancellationToken cancellationToken = default) => Task.FromResult(Description);

    public Task<IReadOnlyList<MigrationSourceInventoryEntry>> Inventory(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MigrationSourceInventoryEntry>>(
            [.. rowsByCategory.Select(pair => new MigrationSourceInventoryEntry("memory", pair.Key, pair.Value.Count))]);

    public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) =>
        Task.FromResult<long>(rowsByCategory.TryGetValue(category.Id, out var rows) ? rows.Count : 0);

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

        int attempt;

        lock (bodyReadGate)
        {
            attempt = bodyReadAttempts[sourceId] = bodyReadAttempts.GetValueOrDefault(sourceId) + 1;
            PeakBodyReadsInFlight = Math.Max(PeakBodyReadsInFlight, Interlocked.Increment(ref bodyReadsInFlight));
        }

        try
        {
            if (heldBodyReads is { } held)
            {
                await held.Task;
            }

            if (StopOnBodyRead is { } stop && stop.SourceId == sourceId)
            {
                await stop.Source.CancelAsync();
                throw new OperationCanceledException(stop.Source.Token);
            }

            if (bodyFailures.TryGetValue(sourceId, out var failures) && attempt <= failures.Times)
            {
                throw failures.Failure;
            }

            if (bodyFailuresInTurn.TryGetValue(sourceId, out var failuresInTurn) && attempt <= failuresInTurn.Length)
            {
                throw failuresInTurn[attempt - 1];
            }

            return bodies.GetValueOrDefault(sourceId);
        }
        finally
        {
            Interlocked.Decrement(ref bodyReadsInFlight);
        }
    }

    // Every registered category, not just the seeded ones: Seed writes rowsByCategory after construction,
    // and the interface promises an answer that does not change across Open.
    public IReadOnlyCollection<string> SupportedCategoryIds => [.. MigrationCategoryRegistry.All.Select(category => category.Id)];

    public IReadOnlyDictionary<string, Type> DocumentTypes => MigrationCategoryRegistry.All.ToDictionary(category => category.Id, _ => typeof(object));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
