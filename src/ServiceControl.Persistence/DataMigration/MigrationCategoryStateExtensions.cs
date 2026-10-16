namespace ServiceControl.Persistence.DataMigration;

public static class MigrationCategoryStateExtensions
{
    /// <summary>
    /// Whether the category is done with, so a restart passes over it and it no longer holds the host back: Done,
    /// or Abandoned by the operator. A Failed category (<see cref="IsFailed" />) is not finished. It waits for
    /// --migration-retry or --migration-abandon, and a category that must follow it stays blocked until then.
    /// </summary>
    public static bool IsFinished(this MigrationCategoryState state) =>
        state is MigrationCategoryState.Complete
              or MigrationCategoryState.Abandoned;

    /// <summary>
    /// Whether the category is Failed: it stopped early, or reached the end having skipped rows to faults. It waits
    /// for the operator to run --migration-retry or --migration-abandon, and no start copies it until then.
    /// </summary>
    public static bool IsFailed(this MigrationCategoryState state) =>
        state is MigrationCategoryState.Halted
              or MigrationCategoryState.CompleteWithErrors;
}

public static class MigrationSkipReasonExtensions
{
    /// <summary>
    /// Whether the running product would have dropped this row anyway, which makes the skip no loss. The halt
    /// threshold and the settle rule never count a harmless skip, so no number of them can stop a category or
    /// leave it Failed. The list is fixed. <see cref="MigrationSkipReason.Unknown" /> is not on it, because a
    /// newer build's reason read back here may be a real loss.
    /// </summary>
    public static bool IsBenign(this MigrationSkipReason reason) =>
        reason is MigrationSkipReason.PastRetention
               or MigrationSkipReason.BlankGroupComment;

    /// <summary>
    /// Whether no retry can fix a row skipped for this reason, because the row itself can never be stored.
    /// </summary>
    public static bool IsPermanent(this MigrationSkipReason reason) => reason is MigrationSkipReason.RequiredValueMissing;
}
