namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed record MigrationCategoryStatus(
    MigrationCategory Category,
    bool Selected,
    MigrationCategoryState State,
    long CopiedCount,
    long SkippedCount,
    long AlreadyPresentCount,
    DateTime? StartedAt,
    DateTime? LastProgressAt,
    DateTime? SettledAt,
    string? LastError,
    bool CanBeRetried)
{
    public string CustomerState => State.CustomerState();

    // The guided run offers retry and abandon only for a Failed required category, and CanBeRetried comes from MigrationCheckpointRules.
    // The arguments refuse an id this build does not know, so such a row is sent to the build that wrote it.
    public string? NextCommands =>
        Category.Kind != MigrationCategoryKind.Required || !CanBeRetried ? null
        : MigrationCategoryRegistry.Find(Category.Id) is null ? $"--start-data-migration with the build that created {Category.Id}, which this build does not know"
        : $"--start-data-migration --retry={Category.Id}  or  --start-data-migration --abandon={Category.Id}";
}

public static class MigrationStatusView
{
    public static IReadOnlyList<MigrationCategoryStatus> Build(IReadOnlyList<MigrationCheckpoint> checkpoints, IReadOnlyCollection<string> selectedOptionalCategoryIds)
    {
        var byId = checkpoints.ToDictionary(checkpoint => checkpoint.CategoryId);

        return [.. MigrationCategoryRegistry.WithUnknownRows(checkpoints).Select(category =>
        {
            // Selection comes from the settings, because a selected category nothing has started has no row, and an old row may come from a wider selection.
            var selected = category.Kind == MigrationCategoryKind.Required || selectedOptionalCategoryIds.Contains(category.Id);

            var checkpoint = byId.GetValueOrDefault(category.Id);

            // A category nothing will copy is not outstanding, so it is offered nothing.
            var outstanding = checkpoint?.IsOutstanding(category.Kind, selected) ?? selected;
            var canBeRetried = outstanding && checkpoint.CanBeRetried();

            return checkpoint is not null
                ? new MigrationCategoryStatus(category, selected, checkpoint.State, checkpoint.CopiedCount, checkpoint.SkippedCount, checkpoint.AlreadyPresentCount, checkpoint.StartedAt, checkpoint.LastProgressAt, checkpoint.SettledAt, checkpoint.LastError, canBeRetried)
                : new MigrationCategoryStatus(category, selected, MigrationCategoryState.NotStarted, 0, 0, 0, null, null, null, null, canBeRetried);
        })];
    }
}
