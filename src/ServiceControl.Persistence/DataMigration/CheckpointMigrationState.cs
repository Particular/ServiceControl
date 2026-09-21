namespace ServiceControl.Persistence.DataMigration;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Answers <see cref="IMigrationState"/> from the saved checkpoints, read once before the host opens rather than queried live.
/// </summary>
public sealed class CheckpointMigrationState(IMigrationCheckpointStore? checkpointStore = null) : IMigrationState
{
    volatile bool anyCategoryIncomplete;
    IReadOnlyCollection<string> selectedCategoryIds = [];

    public bool AnyCategoryIncomplete => anyCategoryIncomplete;

    /// <summary>
    /// Reads every checkpoint and fixes the answer for the life of the host.
    /// </summary>
    /// <param name="selectedIds">The categories this instance was asked to copy. They are passed in because the store holds a row only for a category some run has already started.</param>
    public async Task Seed(IReadOnlyCollection<string> selectedIds, CancellationToken cancellationToken = default)
    {
        // Only a persister that can be migrated into keeps checkpoints, so no store means no migration has run.
        if (checkpointStore is null)
        {
            return;
        }

        selectedCategoryIds = selectedIds;

        Recompute(await checkpointStore.ReadAll(cancellationToken));
    }

    /// <summary>
    /// Works the answer out again from the checkpoints given, without reading the store.
    /// </summary>
    /// <param name="checkpoints">Every checkpoint the store holds. A selected category with no checkpoint among them has not started, which is not finished.</param>
    public void Recompute(IReadOnlyList<MigrationCheckpoint> checkpoints) =>
        anyCategoryIncomplete = selectedCategoryIds.Any(id =>
            checkpoints.FirstOrDefault(checkpoint => checkpoint.CategoryId == id)?.State.IsFinished() != true);
}
