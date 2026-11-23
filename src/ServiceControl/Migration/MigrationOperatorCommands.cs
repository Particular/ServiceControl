#nullable enable
namespace ServiceControl.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// What the operator commands do to one category's checkpoint, run with ServiceControl stopped.
/// </summary>
static class MigrationOperatorCommands
{
    /// <summary>
    /// The reset a retry saves: back to NotStarted with the cursor, counts, skip reasons, settle time and error
    /// cleared, so the next copy reads the whole category again. It keeps StartedAt and LastProgressAt, and throws InvalidOperationException for a category that cannot be retried.
    /// </summary>
    public static MigrationCheckpoint Retry(MigrationCheckpoint checkpoint)
    {
        if (!checkpoint.CanBeRetried())
        {
            throw new InvalidOperationException($"{checkpoint.CategoryId} is {checkpoint.State.CustomerState()}, and only a Failed category can be retried.");
        }

        return checkpoint with
        {
            State = MigrationCategoryState.NotStarted,
            Cursor = null,
            CopiedCount = 0,
            SkippedCount = 0,
            AlreadyPresentCount = 0,
            SkipReasons = null,
            SettledAt = null,
            LastError = null
        };
    }

    /// <summary>
    /// Reads the category's checkpoint and hands its retry reset to the target, against the version that read
    /// returned. A copy that saved the row in between makes it throw MigrationCheckpointConflictException.
    /// </summary>
    public static async Task<MigrationCheckpoint> ApplyRetry(IMigrationCheckpointStore checkpointStore, IMigrationTarget target, string categoryId, CancellationToken cancellationToken = default)
    {
        var checkpoint = await checkpointStore.Read(categoryId, cancellationToken)
            ?? throw new InvalidOperationException($"No migration has started {categoryId}, so there is nothing to retry.");

        return await target.ResetForRetry(Retry(checkpoint), cancellationToken);
    }

    /// <summary>
    /// The row an abandon saves: Abandoned, settled at <paramref name="utcNow" />, with the cursor, counts, skip reasons and
    /// error kept as the size of the loss and its cause. Throws InvalidOperationException for a category that cannot be abandoned.
    /// </summary>
    public static MigrationCheckpoint Abandon(MigrationCheckpoint checkpoint, DateTime utcNow) =>
        checkpoint.CanBeAbandoned()
            ? checkpoint with { State = MigrationCategoryState.Abandoned, SettledAt = utcNow }
            : throw new InvalidOperationException(WhyItCannotBeAbandoned(checkpoint));

    static string WhyItCannotBeAbandoned(MigrationCheckpoint checkpoint) =>
        $"{checkpoint.CategoryId} is {checkpoint.State.CustomerState()} and cannot be abandoned.";

    /// <summary>
    /// The categories whose rows depend on this one's rows, through data links only and at any depth, deepest first.
    /// </summary>
    public static IReadOnlyList<string> CategoriesThatNeedItsData(string categoryId, IReadOnlyList<MigrationCategory> categories)
    {
        var dependants = new List<string>();

        foreach (var dependant in categories.Where(category => category.MustFollow == categoryId && category.MustFollowIsDataLink))
        {
            dependants.AddRange(CategoriesThatNeedItsData(dependant.Id, categories));
            dependants.Add(dependant.Id);
        }

        return dependants;
    }

    /// <summary>
    /// Abandons the category and every unfinished category that needs its data, saving the named one last so a stopped
    /// command can be run again. Returns the rows it saved, dependants first, and logs each one. A copy still writing to the
    /// named row makes it throw MigrationCheckpointConflictException before any dependant is abandoned.
    /// </summary>
    public static async Task<IReadOnlyList<MigrationCheckpoint>> ApplyAbandon(IMigrationCheckpointStore checkpointStore, string categoryId, DateTime utcNow, ILogger logger, CancellationToken cancellationToken = default)
    {
        var named = await checkpointStore.Read(categoryId, cancellationToken);

        if (!named.CanBeAbandoned())
        {
            throw new InvalidOperationException(WhyItCannotBeAbandoned(named));
        }

        // Saved unchanged to move its version. A copy still running on it then fails on its next batch.
        if (named is not null)
        {
            named = await checkpointStore.Upsert(named, cancellationToken);
        }

        var abandoned = new List<MigrationCheckpoint>();

        foreach (var dependantId in CategoriesThatNeedItsData(categoryId, MigrationCategoryRegistry.All))
        {
            var dependant = await checkpointStore.Read(dependantId, cancellationToken);

            if (dependant?.State.IsFinished() == true)
            {
                continue;
            }

            abandoned.Add(await checkpointStore.Upsert(
                dependant is null ? AbandonedRow(dependantId, utcNow) : dependant with { State = MigrationCategoryState.Abandoned, SettledAt = utcNow },
                cancellationToken));
            logger.LogInformation("Abandoned category {CategoryId}, which was {PreviousState}, because it needs the data of {AbandonedCategoryId}", dependantId, StateBefore(dependant), categoryId);
        }

        abandoned.Add(await checkpointStore.Upsert(named is null ? AbandonedRow(categoryId, utcNow) : Abandon(named, utcNow), cancellationToken));
        logger.LogInformation("Abandoned category {CategoryId}, which was {PreviousState}", categoryId, StateBefore(named));
        return abandoned;
    }

    static string StateBefore(MigrationCheckpoint? checkpoint) => checkpoint?.State.ToString() ?? "never started";

    // A category with no row gets an Abandoned one of its own, so no later copy seeds and copies it.
    static MigrationCheckpoint AbandonedRow(string categoryId, DateTime utcNow) =>
        new(categoryId, MigrationCategoryState.Abandoned, null, 0, 0, null, null, null, null, utcNow, null);
}
