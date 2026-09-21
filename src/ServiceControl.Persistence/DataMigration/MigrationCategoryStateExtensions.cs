namespace ServiceControl.Persistence.DataMigration;

public static class MigrationCategoryStateExtensions
{
    /// <summary>
    /// Whether the category is done with, so a restart passes over it and it no longer holds the host back.
    /// <see cref="MigrationCategoryState.Halted" /> is deliberately not finished. A halt says "stopped, and here
    /// is why", and a restart once the cause is fixed has to be able to pick the category up again.
    /// </summary>
    public static bool IsFinished(this MigrationCategoryState state) =>
        state is MigrationCategoryState.Complete
              or MigrationCategoryState.CompleteWithErrors
              or MigrationCategoryState.Abandoned;
}

public static class MigrationSkipReasonExtensions
{
    /// <summary>
    /// Whether the running product would have dropped this row anyway, which makes the skip no loss. The halt
    /// threshold never counts a benign skip, so no number of them can stop a category.
    /// </summary>
    public static bool IsBenign(this MigrationSkipReason reason) => reason is MigrationSkipReason.EndpointNotKnown;
}
