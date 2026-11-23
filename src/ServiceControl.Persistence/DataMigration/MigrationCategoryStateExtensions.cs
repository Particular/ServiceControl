namespace ServiceControl.Persistence.DataMigration;

public static class MigrationCategoryStateExtensions
{
    /// <summary>
    /// Whether the category is done with, so a restart passes over it and it no longer holds the host back: Done,
    /// or Abandoned by the operator. A Failed category (<see cref="IsFailed" />) is not finished. It waits for
    /// the operator to retry or abandon it, and a category that must follow it stays blocked until then.
    /// </summary>
    public static bool IsFinished(this MigrationCategoryState state) =>
        state is MigrationCategoryState.Complete
              or MigrationCategoryState.Abandoned;

    /// <summary>
    /// Whether the category is Failed: it stopped early, or reached the end having skipped rows to faults. It waits
    /// for the operator to retry or abandon it, and no start copies it until then.
    /// </summary>
    public static bool IsFailed(this MigrationCategoryState state) =>
        state is MigrationCategoryState.Halted
              or MigrationCategoryState.CompleteWithErrors;

    /// <summary>
    /// The word the operator reads for a stored state: Done, Abandoned, Failed or Copying.
    /// </summary>
    public static string CustomerState(this MigrationCategoryState state) => state switch
    {
        MigrationCategoryState.Complete => "Done",
        MigrationCategoryState.Abandoned => "Abandoned",
        MigrationCategoryState.Halted or MigrationCategoryState.CompleteWithErrors => "Failed",
        MigrationCategoryState.NotStarted or MigrationCategoryState.InProgress or MigrationCategoryState.Blocked => "Copying",
        _ => state.ToString()
    };
}

public static class MigrationSkipReasonExtensions
{
    /// <summary>
    /// Whether the skip is no loss, because the running product would have dropped the row anyway, or the row
    /// is a result of a check the source's own storage runs about itself, which nothing reports on the target. The halt threshold and the settle rule never count a
    /// harmless skip, so no number of them can stop a category or leave it Failed. The list is fixed, and
    /// <see cref="MigrationSkipReason.Unknown" /> is not on it, because a newer build's reason read back here
    /// may be a real loss.
    /// </summary>
    public static bool IsBenign(this MigrationSkipReason reason) =>
        reason is MigrationSkipReason.PastRetention
               or MigrationSkipReason.BlankGroupComment
               or MigrationSkipReason.SourceStorageCheck;

    /// <summary>
    /// Whether no retry can fix a row skipped for this reason.
    /// </summary>
    public static bool IsPermanent(this MigrationSkipReason reason) =>
        reason is MigrationSkipReason.RequiredValueMissing
               or MigrationSkipReason.UnparseableUniqueMessageId
               or MigrationSkipReason.NoProcessingAttempts
               or MigrationSkipReason.ValueTooLong
               or MigrationSkipReason.MalformedValue
               or MigrationSkipReason.BodyMissing;

    /// <summary>
    /// The words every report prints after the reason's name: ", no retry can fix", ", harmless", or nothing for a fault a retry can fix.
    /// </summary>
    public static string RetryNote(this MigrationSkipReason reason) =>
        reason.IsPermanent() ? ", no retry can fix" : reason.IsBenign() ? ", harmless" : "";
}
