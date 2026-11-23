#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions;
using ServiceControl.Infrastructure;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the failed messages with one of a category's statuses. The archived and resolved messages in the same collection can
/// outnumber the others many times, so it queries <see cref="FailedMessageViewIndex" /> for just these statuses instead of reading them all.
/// </summary>
abstract class FailedMessageStatusReader(RavenReadOnlySourceLifecycle lifecycle, IReadOnlyCollection<FailedMessageStatus> statuses, TimeSpan? indexBudget) : MigrationCategoryReader<FailedMessage>(lifecycle)
{
    /// <summary>
    /// How long a reader that runs after ServiceControl has opened waits for the index before it reads the whole collection. Ten
    /// minutes is a chosen tolerance, not a measured catch-up time: a large index rebuild can take longer, and then the slower read, which the
    /// copy's throttle paces and no outage depends on, is accepted rather than leaving the unresolved messages uncopied.
    /// </summary>
    protected static readonly TimeSpan OnlineIndexBudget = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Waits for <see cref="FailedMessageViewIndex" /> to catch up, up to the reader's index budget, then streams the messages with the category's statuses in id
    /// order, starting after <paramref name="resumeAfter" />. When the index is missing, errored or disabled, or does not hold
    /// every failed message with a status some category copies, or when the budget runs out first, it logs a warning and reads the
    /// whole collection instead. A reader with no budget leaves cancellation to its caller, so slow indexing alone does not
    /// turn the required copy's outage into a read of the whole archive.
    /// Both ways use the document id as the cursor, so a copy can switch from one to the other between runs.
    /// Throws <see cref="InvalidOperationException" /> when the source holds no document with the id in <paramref name="resumeAfter" />,
    /// or when a failed message has a status no category copies.
    /// </summary>
    protected async IAsyncEnumerable<MigrationBatch> ReadStatuses(string? resumeAfter, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var databaseName = Lifecycle.Settings.DatabaseName;
        var project = new FailedMessageStream(Lifecycle).Project(statuses);

        var batches = await IndexHoldsEveryCopiedFailedMessage(databaseName, cancellationToken)
            ? ThroughIndex(databaseName, resumeAfter, batchSize, project, cancellationToken)
            : Documents($"{FailedMessageIdGenerator.CollectionName}/", resumeAfter, batchSize, project, cancellationToken);

        await foreach (var batch in batches)
        {
            yield return batch;
        }
    }

    async IAsyncEnumerable<MigrationBatch> ThroughIndex(
        string databaseName,
        string? resumeAfter,
        int batchSize,
        Func<StreamResult<FailedMessage>, MigrationRow?> project,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var session = Lifecycle.OpenSession(databaseName);

        await RavenDocumentStream.RefuseMissingCursor(session, CategoryId, databaseName, resumeAfter, cancellationToken);

        var query = Query(session).WhereIn(nameof(FailedMessage.Status), StatusValues(statuses));

        if (resumeAfter is not null)
        {
            query = query.AndAlso().WhereGreaterThan(DocumentId, resumeAfter);
        }

        await using var enumerator = await session.Advanced.StreamAsync(query.OrderBy(DocumentId).OfType<FailedMessage>(), cancellationToken);

        await foreach (var batch in RavenDocumentStream.Batches(enumerator, resumeAfter, batchSize, RavenDocumentStream.MaxDocumentsWalkedPerBatch, project))
        {
            yield return batch;
        }
    }

    // A full read passes a document RavenDB could not index to the target to skip, and refuses a status no category copies,
    // but a query on the index sees neither, so either kind of document sends the copy back to a full read.
    // The budget covers the whole check, the waits, the repeats on a stale count and the requests in between alike. It cancels a
    // token linked to the caller's, and only a cancellation the caller did not ask for becomes the fallback.
    async Task<bool> IndexHoldsEveryCopiedFailedMessage(string databaseName, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (indexBudget is { } limit)
        {
            budget.CancelAfter(limit);
        }

        try
        {
            return await IndexHoldsEveryCopiedFailedMessageWithin(databaseName, budget.Token);
        }
#pragma warning disable PS0020 // The try runs on the budget's linked token, so the caller's own token is the one that must not be set for this to be the budget's cancellation
        catch (OperationCanceledException) when (budget.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
#pragma warning restore PS0020
        {
            logger.LogWarning("The RavenDB index {IndexName} in {DatabaseName} was still behind after {Budget}, so copying {CategoryId} reads every failed message instead, archived and resolved ones included, which makes the copy slower. If indexing is paused, resume it in RavenDB Studio", IndexName, databaseName, indexBudget, CategoryId);
            return false;
        }
    }

    async Task<bool> IndexHoldsEveryCopiedFailedMessageWithin(string databaseName, CancellationToken cancellationToken)
    {
        using var session = Lifecycle.OpenSession(databaseName);

        while (true)
        {
            var index = await WaitForIndex(databaseName, cancellationToken);

            if (index is null || index.State is IndexState.Error or IndexState.Disabled)
            {
                logger.LogWarning("The RavenDB index {IndexName} in {DatabaseName} is {IndexState}, so copying {CategoryId} reads every failed message instead, archived and resolved ones included, which makes the copy slower", IndexName, databaseName, index?.State.ToString() ?? "missing", CategoryId);
                return false;
            }

            // Expiration keeps deleting archived and resolved messages during the copy, so the collection is counted before and
            // after the index count, because a deletion in between could hide a document the index left out. A stale index count
            // means something changed after the wait, so it waits and counts again.
            var storedBefore = await StoredFailedMessages(databaseName, cancellationToken);
            int indexed;
            QueryStatistics statistics;
            try
            {
                indexed = await Query(session).Statistics(out statistics).WhereIn(nameof(FailedMessage.Status), StatusValues(FailedMessageStream.CopiedStatuses)).CountAsync(cancellationToken);
            }
            catch (RavenException)
            {
                // RavenDB can mark the index errored after the wait saw it working, and then refuses the query. The next pass falls back to a full read.
                if ((await WaitForIndex(databaseName, cancellationToken))?.State is IndexState.Error or IndexState.Disabled)
                {
                    continue;
                }

                throw;
            }

            var storedAfter = await StoredFailedMessages(databaseName, cancellationToken);

            if (statistics.IsStale)
            {
                await Task.Delay(PollEvery, cancellationToken);
                continue;
            }

            if (indexed == storedBefore && indexed == storedAfter)
            {
                return true;
            }

            logger.LogWarning("The RavenDB database {DatabaseName} held {StoredBefore} and then {StoredAfter} failed messages while its index {IndexName} held {Indexed} with a status a category copies, so copying {CategoryId} reads every failed message instead, archived and resolved ones included, which makes the copy slower. The counts differ when RavenDB could not index a document, which the index errors in RavenDB Studio name, when a document has a status no category copies, or when expiration deleted documents during the count", databaseName, storedBefore, storedAfter, IndexName, indexed, CategoryId);
            return false;
        }
    }

    async Task<long> StoredFailedMessages(string databaseName, CancellationToken cancellationToken) =>
        (await Lifecycle.DocumentStore.Maintenance.ForDatabase(databaseName).SendAsync(new GetCollectionStatisticsOperation(), cancellationToken))
        .Collections.GetValueOrDefault(FailedMessageIdGenerator.CollectionName);

    // A query on an index that is behind leaves out what it has not indexed yet, which here means unresolved messages nothing
    // would copy. It waits rather than halts, because halting would need an operator to retry for a wait that is normally seconds.
    async Task<IndexInformation?> WaitForIndex(string databaseName, CancellationToken cancellationToken)
    {
        var waiting = Stopwatch.StartNew();
        var nextLog = TimeSpan.Zero;

        while (true)
        {
            var statistics = await Lifecycle.DocumentStore.Maintenance.ForDatabase(databaseName).SendAsync(new GetStatisticsOperation(), cancellationToken);
            var index = statistics.Indexes.SingleOrDefault(candidate => candidate.Name == IndexName);

            // A missing, errored or disabled index never catches up, so waiting for it would never end.
            if (index is null || !index.IsStale || index.State is IndexState.Error or IndexState.Disabled)
            {
                return index;
            }

            if (waiting.Elapsed >= nextLog)
            {
                await LogWait(databaseName, waiting.Elapsed, cancellationToken);
                nextLog += LogEvery;
            }

            await Task.Delay(PollEvery, cancellationToken);
        }
    }

    // A paused index stays stale with its state still Normal, so without this the log reads exactly like an index that is merely slow.
    async Task LogWait(string databaseName, TimeSpan waited, CancellationToken cancellationToken)
    {
        var indexing = await Lifecycle.DocumentStore.Maintenance.ForDatabase(databaseName).SendAsync(new GetIndexingStatusOperation(), cancellationToken);
        var paused = indexing.Status == IndexRunningStatus.Paused
            || indexing.Indexes.Any(index => index.Name == IndexName && index.Status == IndexRunningStatus.Paused);

        if (paused)
        {
            if (indexBudget is { } limit)
            {
                logger.LogWarning("Copying {CategoryId} is waiting for the RavenDB index {IndexName} in {DatabaseName}, but indexing is paused, so the copy waits up to {Budget} for it to be resumed in RavenDB Studio and then reads every failed message instead. Waited {Waited} so far", CategoryId, IndexName, databaseName, limit, waited);
                return;
            }

            logger.LogWarning("Copying {CategoryId} is waiting for the RavenDB index {IndexName} in {DatabaseName}, but indexing is paused, so the copy waits until it is resumed in RavenDB Studio. Waited {Waited} so far", CategoryId, IndexName, databaseName, waited);
            return;
        }

        logger.LogInformation("Waiting for the RavenDB index {IndexName} in {DatabaseName} to catch up before copying {CategoryId}, waited {Waited} so far", IndexName, databaseName, CategoryId, waited);
    }

    static IAsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions> Query(IAsyncDocumentSession session) =>
        session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>();

    static object[] StatusValues(IEnumerable<FailedMessageStatus> statuses) => statuses.Select(status => (object)(int)status).ToArray();

    static readonly string IndexName = new FailedMessageViewIndex().IndexName;
    static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(1);
    static readonly TimeSpan LogEvery = TimeSpan.FromSeconds(30);
    static readonly ILogger<FailedMessageStatusReader> logger = LoggerUtil.CreateStaticLogger<FailedMessageStatusReader>();

    const string DocumentId = "id()";
}
