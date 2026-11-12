#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Session;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads RavenDB documents for a migration category in id order and hands them back in batches, most often every
/// document whose id starts with a prefix. Id order is what lets a copy resume, because a resume asks RavenDB to
/// start after the last id it copied rather than to skip a count.
/// </summary>
static class RavenDocumentStream
{
    /// <summary>
    /// The most documents one batch reads before it is handed back, even if it holds few rows or none. Every batch is
    /// one commit, so a long run of documents that make no row costs one commit per this many documents rather than one
    /// per batch size, while the cursor is still saved every few seconds and a resume reads at most this many documents again.
    /// </summary>
    public const int MaxDocumentsWalkedPerBatch = 10_000;

    /// <summary>
    /// Streams the documents whose id starts with <paramref name="prefix" />, after <paramref name="resumeAfter" />, and yields a batch as soon as it holds <paramref name="batchSize" /> rows, or once it has read <paramref name="maxDocumentsWalkedPerBatch" /> documents, even if it holds few rows or none. A batch never holds more than <paramref name="batchSize" /> rows, but when <paramref name="project" /> returns null for some documents, one batch can cover more documents than that. If any documents were read after the last such batch, it yields one more batch for them, even an empty one, so the cursor past them is saved. When <paramref name="project" /> never returns null and <paramref name="batchSize" /> is no more than <paramref name="maxDocumentsWalkedPerBatch" />, every batch but the last holds exactly <paramref name="batchSize" /> rows. Throws <see cref="InvalidOperationException" /> when the source holds no document with the id in <paramref name="resumeAfter" />.
    /// </summary>
    public static async IAsyncEnumerable<MigrationBatch> ByPrefix<TDocument>(
        RavenReadOnlySourceLifecycle lifecycle,
        string categoryId,
        string databaseName,
        string prefix,
        string? resumeAfter,
        int batchSize,
        int maxDocumentsWalkedPerBatch,
        Func<StreamResult<TDocument>, MigrationRow?> project,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) where TDocument : class
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDocumentsWalkedPerBatch);

        using var session = lifecycle.OpenSession(databaseName);

        // RavenDB starts a stream after whatever id it is given, even one that does not exist, so a stale cursor would skip rows nothing has copied.
        await RefuseMissingCursor(session, categoryId, databaseName, resumeAfter, cancellationToken);

        await using var enumerator = await session.Advanced.StreamAsync<TDocument>(
            prefix, startAfter: resumeAfter, token: cancellationToken);

        await foreach (var batch in Batches(enumerator, resumeAfter, batchSize, maxDocumentsWalkedPerBatch, project))
        {
            yield return batch;
        }
    }

    /// <summary>
    /// Checks that the cursor names a document the source still holds. The cursor comes from the target's checkpoint, so
    /// if the source has no document with that id, either the copy is now reading a different database or the document was
    /// deleted since, and resuming after it could skip rows that were never copied. Throws
    /// <see cref="InvalidOperationException" /> when <paramref name="resumeAfter" /> is not null and the source holds no document with that id.
    /// </summary>
    public static async Task RefuseMissingCursor(IAsyncDocumentSession session, string categoryId, string databaseName, string? resumeAfter, CancellationToken cancellationToken = default)
    {
        if (resumeAfter is not null && !await session.Advanced.ExistsAsync(resumeAfter, cancellationToken))
        {
            throw new InvalidOperationException(
                $"The migration cannot resume the '{categoryId}' category after '{resumeAfter}', because the RavenDB database '{databaseName}' no longer holds a document with that id. The likeliest cause is that the document expired and RavenDB deleted it while the copy was interrupted, which can happen to any archived or resolved message. Run --migration-retry {categoryId} to copy the category again from the start. If RavenDB was instead restored from a backup or the migration now reads a different database, point it back at the database this copy started from and restart.");
        }
    }

    /// <summary>
    /// Groups the documents from a RavenDB stream into batches, by the rules <see cref="ByPrefix{TDocument}" /> describes:
    /// a batch goes out once it holds <paramref name="batchSize" /> rows or has read <paramref name="maxDocumentsWalkedPerBatch" />
    /// documents, and one last batch goes out for any documents read after that, even if it holds no rows. The stream must return
    /// documents in id order, or a resume would start in the wrong place. <paramref name="resumeAfter" /> is the id the stream
    /// started after, or null. <paramref name="project" /> turns a document into a row, or returns null for a document the category
    /// does not copy, and the cursor still moves past that document. Each batch's cursor is the id of the last document it read.
    /// An empty stream yields nothing.
    /// </summary>
    public static async IAsyncEnumerable<MigrationBatch> Batches<TDocument>(
        IAsyncEnumerator<StreamResult<TDocument>> enumerator,
        string? resumeAfter,
        int batchSize,
        int maxDocumentsWalkedPerBatch,
        Func<StreamResult<TDocument>, MigrationRow?> project)
    {
        var rows = new List<MigrationRow>(batchSize);
        var cursor = resumeAfter;
        var lastYielded = resumeAfter;
        var walkedSinceYield = 0;

        while (await enumerator.MoveNextAsync())
        {
            // The cursor moves on every document the stream hands back, even one that becomes no row, so a resume never walks it again.
            cursor = enumerator.Current.Id;
            walkedSinceYield++;

            if (project(enumerator.Current) is { } row)
            {
                rows.Add(row);
            }

            // Handing back a batch at the document cap saves progress through a long run of documents that make no row.
            if (rows.Count == batchSize || walkedSinceYield == maxDocumentsWalkedPerBatch)
            {
                yield return new MigrationBatch(rows, cursor);
                rows = new List<MigrationRow>(batchSize);
                lastYielded = cursor;
                walkedSinceYield = 0;
            }
        }

        // A tail whose documents all became no row still goes out, empty, so that the cursor past them is saved.
        if (rows.Count > 0 || cursor != lastYielded)
        {
            yield return new MigrationBatch(rows, cursor!);
        }
    }

    public static MigrationRow WholeDocument<TDocument>(StreamResult<TDocument> result) where TDocument : class =>
        new(result.Id, result.Document, NoMetadata);

    static readonly IReadOnlyDictionary<string, object?> NoMetadata = ReadOnlyDictionary<string, object?>.Empty;
}
