namespace ServiceControl.Persistence.EFCore.DataMigration;

using DbContexts;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// A writer that takes rows carrying a <typeparamref name="TDocument" />. It casts every row once, before
/// <see cref="PrepareDocuments" /> sees it, so a row from a reader that yields another type fails the batch by name.
/// </summary>
abstract class MigrationCategoryWriter<TDocument> : IMigrationCategoryWriter where TDocument : class
{
    public abstract string CategoryId { get; }

    public Type DocumentType => typeof(TDocument);

    public abstract int BatchSize(ServiceControlDbContext dbContext);

    public abstract Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default);

    public Task<PreparedBatch> Prepare(ServiceControlDbContext dbContext, MigrationBatch batch, CancellationToken cancellationToken = default) =>
        PrepareDocuments(dbContext, [.. batch.Rows.Select(row => (row, DocumentOf(row)))], cancellationToken);

    /// <summary>
    /// Does what <see cref="IMigrationCategoryWriter.Prepare" /> promises, for rows already cast.
    /// </summary>
    /// <param name="dbContext">The context to read the target through. Nothing may be written through it here.</param>
    /// <param name="documents">Every row in the batch, in source order, each with its document.</param>
    /// <param name="cancellationToken">Cancels any read of the target.</param>
    /// <returns>One prepared row or one skip for every entry in <paramref name="documents" />.</returns>
    protected abstract Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, TDocument Document)> documents, CancellationToken cancellationToken = default);

    TDocument DocumentOf(MigrationRow row) =>
        row.Document as TDocument
        ?? throw new InvalidCastException($"The {CategoryId} writer takes {typeof(TDocument).FullName} documents, but row {row.SourceId} holds {row.Document?.GetType().FullName ?? "no document"}. The reader and writer for one category must agree on the document type.");
}
