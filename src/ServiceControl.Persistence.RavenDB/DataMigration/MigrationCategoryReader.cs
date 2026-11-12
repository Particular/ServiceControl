#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Threading;
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
}
