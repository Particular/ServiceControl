#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads a category that RavenDB keeps in one document with a fixed id.
/// The document is one batch, and its id is the cursor. A missing document gives a batch with no rows.
/// </summary>
abstract class SingleDocumentReader<TStored, TDocument>(RavenReadOnlySourceLifecycle lifecycle, string databaseName, string documentId) : MigrationCategoryReader<TDocument>(lifecycle)
    where TStored : class
    where TDocument : class
{
    /// <summary>
    /// The id of the one document this category reads, and the cursor of its only batch.
    /// </summary>
    protected string DocumentId { get; } = documentId;

    public sealed override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        ReadDocument(resumeAfter, cancellationToken);

    /// <summary>
    /// Turns the loaded document into rows, in the order the writer gets them. Called only when the document exists.
    /// Leave out a missing value rather than return a null document.
    /// </summary>
    protected abstract IEnumerable<(string SourceId, TDocument Document)> RowsFrom(TStored document);

    async IAsyncEnumerable<MigrationBatch> ReadDocument(string? resumeAfter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (resumeAfter == DocumentId)
        {
            yield break;
        }

        if (resumeAfter is not null)
        {
            throw new InvalidOperationException($"The migration cannot resume the '{CategoryId}' category after '{resumeAfter}', because its only cursor is '{DocumentId}'. Run --migration-retry {CategoryId} to copy the category again from the start.");
        }

        using var session = Lifecycle.OpenSession(databaseName);
        var document = await session.LoadAsync<TStored>(DocumentId, cancellationToken);

        yield return new MigrationBatch(
            document is null ? [] : [.. RowsFrom(document).Select(row => new MigrationRow(row.SourceId, row.Document, ReadOnlyDictionary<string, object?>.Empty))],
            DocumentId);
    }
}
