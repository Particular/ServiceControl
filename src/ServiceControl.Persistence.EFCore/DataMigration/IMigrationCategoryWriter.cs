namespace ServiceControl.Persistence.EFCore.DataMigration;

using DbContexts;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Everything the target needs to know about one category: how big a batch is, how its documents
/// become rows, and how many rows the target holds for it.
/// </summary>
interface IMigrationCategoryWriter
{
    /// <summary>The category this writer handles, named as in <see cref="MigrationCategoryIds" />.</summary>
    string CategoryId { get; }

    /// <summary>
    /// The most rows the target will take from the source in one batch. It comes from how many values each row
    /// carries and how many parameters one statement can hold, so a wider table takes fewer rows.
    /// </summary>
    int BatchSize { get; }

    /// <summary>
    /// Turns one batch of source documents into the rows to insert and the rows to skip, writing nothing: the insert it returns runs later, inside the target's transaction.
    /// Every batch row produces exactly one row or exactly one skip, never both and never neither, because the target works out how many were already there by subtracting both from the batch size.
    /// </summary>
    Task<PreparedBatch> Prepare(ServiceControlDbContext dbContext, MigrationBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many rows the target holds for this category. It counts only this category, even where two of them
    /// share a table.
    /// </summary>
    Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default);
}
