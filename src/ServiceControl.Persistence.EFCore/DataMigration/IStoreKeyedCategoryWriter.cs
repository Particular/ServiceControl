namespace ServiceControl.Persistence.EFCore.DataMigration;

using DbContexts;

/// <summary>
/// A category writer whose rows a re-read cannot recognise as already copied, so copying the category again would add every row a second time.
/// Rows whose keys the database makes are one kind, and throughput days, whose counts the copy adds to, are another.
/// The target deletes those rows when it resets the category for a retry.
/// The delete empties whole tables, so it is only safe for a category copied while ServiceControl is stopped.
/// </summary>
interface IStoreKeyedCategoryWriter
{
    /// <summary>
    /// Deletes every row in this category's tables and returns how many it deleted.
    /// Runs in the same transaction as the checkpoint reset.
    /// </summary>
    Task<int> DeleteCopiedRows(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default);
}
