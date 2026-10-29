#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Raven.Client.Documents.Commands;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// The one way every category reads RavenDB: stream the documents whose id starts with a prefix, in id order,
/// and hand them back in batches. Id order is what makes the cursor work, because a resume asks RavenDB to
/// start after an id rather than to skip a count.
/// </summary>
static class RavenDocumentStream
{
    /// <summary>
    /// Streams one collection and yields it in batches of at most <paramref name="batchSize" />.
    /// </summary>
    /// <param name="prefix">The document id prefix that selects the collection, such as "KnownEndpoints/".</param>
    /// <param name="resumeAfter">The cursor a previous run saved, or null to start at the beginning.</param>
    /// <param name="project">Turns one document into a row, or into null for a document this category does not copy. The cursor still moves past a document that becomes null.</param>
    /// <exception cref="InvalidOperationException">The source holds no document with the id in <paramref name="resumeAfter" />, which means the cursor and the database no longer belong together.</exception>
    public static async IAsyncEnumerable<MigrationBatch> ByPrefix<TDocument>(
        RavenReadOnlySourceLifecycle lifecycle,
        string categoryId,
        string databaseName,
        string prefix,
        string? resumeAfter,
        int batchSize,
        Func<StreamResult<TDocument>, MigrationRow?> project,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) where TDocument : class
    {
        using var session = lifecycle.OpenSession(databaseName);

        // RavenDB starts a stream after whatever id it is given, even one that does not exist, so a stale cursor would skip rows nothing has copied.
        if (resumeAfter is not null && !await session.Advanced.ExistsAsync(resumeAfter, cancellationToken))
        {
            throw new InvalidOperationException(
                $"The migration cannot resume the '{categoryId}' category after '{resumeAfter}', because the RavenDB database '{databaseName}' holds no document with that id. The cursor is saved in the target database and names a source document, so restoring RavenDB from a backup or changing which database it reads separates the two. Point the migration back at the RavenDB database this copy started from and restart.");
        }

        await using var enumerator = await session.Advanced.StreamAsync<TDocument>(
            prefix, startAfter: resumeAfter, token: cancellationToken);

        var rows = new List<MigrationRow>(batchSize);
        var cursor = resumeAfter;
        var lastYielded = resumeAfter;

        while (await enumerator.MoveNextAsync())
        {
            // The cursor moves on every document the stream hands back, even one that becomes no row, so a resume never walks it again.
            cursor = enumerator.Current.Id;

            if (project(enumerator.Current) is { } row)
            {
                rows.Add(row);
            }

            if (rows.Count == batchSize)
            {
                yield return new MigrationBatch(rows, cursor);
                rows = new List<MigrationRow>(batchSize);
                lastYielded = cursor;
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
