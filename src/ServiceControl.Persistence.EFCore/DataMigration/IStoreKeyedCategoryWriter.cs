namespace ServiceControl.Persistence.EFCore.DataMigration;

using DbContexts;

/// <summary>
/// A category writer whose rows get their keys from the database, so a re-read would add every row again.
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
