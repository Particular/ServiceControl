#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents.Commands;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// A reader whose every row carries a <typeparamref name="TDocument" />, which is the type its category's writer
/// must take. Reading through <see cref="WholeDocuments" /> ties the documents RavenDB streams to that type.
/// </summary>
abstract class MigrationCategoryReader<TDocument>(RavenReadOnlySourceLifecycle lifecycle) : IMigrationCategoryReader where TDocument : class
{
    /// <summary>
    /// The source connection, for a reader that streams or opens sessions itself instead of using <see cref="WholeDocuments" />.
    /// </summary>
    protected RavenReadOnlySourceLifecycle Lifecycle { get; } = lifecycle;

    public abstract string CategoryId { get; }

    public Type DocumentType => typeof(TDocument);

    public abstract IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default);

    // Counted through Read, not from RavenDB's collection statistics, because a collection does not map one to one onto a category.
    public virtual async Task<long> Count(CancellationToken cancellationToken = default)
    {
        var total = 0L;

        await foreach (var batch in Read(resumeAfter: null, batchSize: CountBatchSize, cancellationToken))
        {
            total += batch.Rows.Count;
        }

        return total;
    }

    /// <summary>
    /// Streams the primary database's documents whose id starts with <paramref name="prefix" />, each one whole as a row.
    /// </summary>
    protected IAsyncEnumerable<MigrationBatch> WholeDocuments(string prefix, string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        Documents(prefix, resumeAfter, batchSize, RavenDocumentStream.WholeDocument, cancellationToken);

    /// <summary>
    /// Streams the primary database's documents whose id starts with <paramref name="prefix" />, in id order, through
    /// <see cref="RavenDocumentStream.ByPrefix{TDocument}" /> with this category's id and the walk limit every reader shares,
    /// <see cref="RavenDocumentStream.MaxDocumentsWalkedPerBatch" />.
    /// </summary>
    protected IAsyncEnumerable<MigrationBatch> Documents(string prefix, string? resumeAfter, int batchSize, Func<StreamResult<TDocument>, MigrationRow?> project, CancellationToken cancellationToken = default) =>
        RavenDocumentStream.ByPrefix(
            Lifecycle,
            CategoryId,
            Lifecycle.Settings.DatabaseName,
            prefix,
            resumeAfter,
            batchSize,
            RavenDocumentStream.MaxDocumentsWalkedPerBatch,
            project,
            cancellationToken);

    // Counting only adds up row counts, so this size changes nothing but how often the stream stops to hand one back.
    const int CountBatchSize = 1024;
}
