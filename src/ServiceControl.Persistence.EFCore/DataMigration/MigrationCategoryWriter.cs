namespace ServiceControl.Persistence.EFCore.DataMigration;

using DbContexts;
using Infrastructure;
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
    protected abstract Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, TDocument Document)> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a skip for a row whose entity holds a text value the database would refuse, because one such value fails the whole statement the row goes into,
    /// or null when every value can be stored. The reason is <see cref="MigrationSkipReason.ValueTooLong" /> for a value longer than its column allows, which is checked first,
    /// or <see cref="MigrationSkipReason.MalformedValue" /> for a value holding a character the provider cannot store.
    /// </summary>
    protected static (string SourceId, MigrationSkipReason Reason, string Detail)? UnstorableValueSkip<TEntity>(MigrationInsert<TEntity> insert, IMigrationSqlDialect dialect, MigrationRow row, TEntity entity) where TEntity : class =>
        insert.ValueTooLongIn(entity, dialect) is { } tooLong
            ? (row.SourceId, MigrationSkipReason.ValueTooLong, $"{tooLong}, on document '{row.SourceId}'")
            : insert.RejectedCharacterIn(entity, dialect) is { } rejected
                ? (row.SourceId, MigrationSkipReason.MalformedValue, $"{rejected}, on document '{row.SourceId}'")
                : null;

    TDocument DocumentOf(MigrationRow row) =>
        row.Document as TDocument
        ?? throw new InvalidCastException($"The {CategoryId} writer takes {typeof(TDocument).FullName} documents, but row {row.SourceId} holds {row.Document?.GetType().FullName ?? "no document"}. The reader and writer for one category must agree on the document type.");
}
