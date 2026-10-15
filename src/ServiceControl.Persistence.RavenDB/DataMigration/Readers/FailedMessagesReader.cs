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
using ServiceControl.Infrastructure;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the failed messages that are still waiting on somebody: unresolved, or with a retry issued. This category is
/// copied while ServiceControl is closed, and the archived and resolved messages in the same collection can outnumber it
/// many times, so it queries <see cref="FailedMessageViewIndex" /> for just these statuses instead of reading them all.
/// </summary>
sealed class FailedMessagesReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<FailedMessage>(lifecycle)
{
    public override string CategoryId => MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages;

    /// <summary>
    /// Waits for <see cref="FailedMessageViewIndex" /> to catch up, then streams the unresolved and retry issued messages in id
    /// order, starting after <paramref name="resumeAfter" />. When the index is missing, errored or disabled, or does not hold
    /// every failed message with a status some category copies, it logs a warning and reads the whole collection instead.
    /// Both ways use the document id as the cursor, so a copy can switch from one to the other between runs.
    /// Throws <see cref="InvalidOperationException" /> when the source holds no document with the id in <paramref name="resumeAfter" />,
    /// or when a failed message has a status no category copies.
    /// </summary>
    public override async IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var databaseName = Lifecycle.Settings.DatabaseName;
        var project = new FailedMessageStream(Lifecycle).Project(MigrationCategoryRegistry.UnresolvedAndRetryIssuedStatuses);

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

        var query = Query(session).WhereIn(nameof(FailedMessage.Status), StatusValues(MigrationCategoryRegistry.UnresolvedAndRetryIssuedStatuses));

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
    async Task<bool> IndexHoldsEveryCopiedFailedMessage(string databaseName, CancellationToken cancellationToken)
    {
        using var session = Lifecycle.OpenSession(databaseName);

        while (true)
        {
            var index = await WaitForIndex(databaseName, cancellationToken);

            if (index is null || index.State is IndexState.Error or IndexState.Disabled)
            {
                logger.LogWarning("The RavenDB index {IndexName} in {DatabaseName} is {IndexState}, so copying {CategoryId} reads every failed message instead, archived and resolved ones included, which keeps ServiceControl closed for longer", IndexName, databaseName, index?.State.ToString() ?? "missing", CategoryId);
                return false;
            }

            // Expiration keeps deleting archived and resolved messages during the copy, so the collection is counted before and
            // after the index count, because a deletion in between could hide a document the index left out. A stale index count
            // means something changed after the wait, so it waits and counts again.
            var storedBefore = await StoredFailedMessages(databaseName, cancellationToken);
            var indexed = await Query(session).Statistics(out var statistics).WhereIn(nameof(FailedMessage.Status), StatusValues(CopiedStatuses)).CountAsync(cancellationToken);
            var storedAfter = await StoredFailedMessages(databaseName, cancellationToken);

            if (statistics.IsStale)
            {
                continue;
            }

            if (indexed == storedBefore && indexed == storedAfter)
            {
                return true;
            }

            logger.LogWarning("The RavenDB database {DatabaseName} held {StoredBefore} and then {StoredAfter} failed messages while its index {IndexName} held {Indexed} with a status a category copies, so copying {CategoryId} reads every failed message instead, archived and resolved ones included, which keeps ServiceControl closed for longer. The counts differ when RavenDB could not index a document, which the index errors in RavenDB Studio name, when a document has a status no category copies, or when expiration deleted documents during the count", databaseName, storedBefore, storedAfter, IndexName, indexed, CategoryId);
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

            // A missing, errored or disabled index never catches up, so waiting for it would keep ServiceControl closed for good.
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
            logger.LogWarning("Copying {CategoryId} is waiting for the RavenDB index {IndexName} in {DatabaseName}, but indexing is paused, so ServiceControl stays closed until it is resumed in RavenDB Studio. Waited {Waited} so far", CategoryId, IndexName, databaseName, waited);
            return;
        }

        logger.LogInformation("Waiting for the RavenDB index {IndexName} in {DatabaseName} to catch up before copying {CategoryId}, waited {Waited} so far", IndexName, databaseName, CategoryId, waited);
    }

    static IAsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions> Query(IAsyncDocumentSession session) =>
        session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>();

    static object[] StatusValues(IEnumerable<FailedMessageStatus> statuses) => statuses.Select(status => (object)(int)status).ToArray();

    static readonly FailedMessageStatus[] CopiedStatuses = [.. MigrationCategoryRegistry.UnresolvedAndRetryIssuedStatuses, .. MigrationCategoryRegistry.ArchivedAndResolvedStatuses];
    static readonly string IndexName = new FailedMessageViewIndex().IndexName;
    static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(1);
    static readonly TimeSpan LogEvery = TimeSpan.FromSeconds(30);
    static readonly ILogger<FailedMessagesReader> logger = LoggerUtil.CreateStaticLogger<FailedMessagesReader>();

    const string DocumentId = "id()";
}
