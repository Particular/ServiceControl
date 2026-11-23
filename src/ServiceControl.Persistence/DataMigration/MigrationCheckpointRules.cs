namespace ServiceControl.Persistence.DataMigration;

using System.Diagnostics.CodeAnalysis;

public static class MigrationCheckpointRules
{
    /// <summary>
    /// Whether the category is still work left before the migration is finished: it is not Complete or Abandoned,
    /// unless it is an optional category that never started and is not selected. <paramref name="selected" /> is
    /// true when the category's window is above zero, and is ignored for a required category.
    /// </summary>
    public static bool IsOutstanding(this MigrationCheckpoint checkpoint, MigrationCategoryKind kind, bool selected) =>
        !checkpoint.State.IsFinished()
        && !(kind == MigrationCategoryKind.Optional
            && checkpoint.State == MigrationCategoryState.NotStarted
            && checkpoint.StartedAt is null
            && !selected);

    /// <summary>
    /// Whether a retry takes the category: a Failed row. Never a missing row.
    /// </summary>
    public static bool CanBeRetried(this MigrationCheckpoint? checkpoint) =>
        checkpoint is not null && checkpoint.State.IsFailed();

    /// <summary>
    /// Whether an abandon takes the category: any state but Complete or Abandoned, even with no row.
    /// A null checkpoint means the category has no row.
    /// </summary>
    public static bool CanBeAbandoned([NotNullWhen(false)] this MigrationCheckpoint? checkpoint) =>
        checkpoint is null || !checkpoint.State.IsFinished();
}
