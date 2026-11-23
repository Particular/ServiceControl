namespace ServiceControl.Migration.Guided;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceControl.Hosting.Commands;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Offers retry, abandon or stop for each Failed required category, copies again after any change, and throws
/// once a pass changes nothing and a category is still unfinished. Without a console it uses only the arguments.
/// </summary>
sealed class FailedCategoriesStep : IMigrationStep
{
    public string Title => "Failed categories";

    public bool AppliesTo(MigrationRun run) => run.Finished?.Any(checkpoint => checkpoint.State.IsFailed()) == true;

    public async Task<int?> Run(MigrationRun run, CancellationToken cancellationToken = default)
    {
        var retriedFromArguments = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            var changed = false;

            foreach (var failed in run.Finished.Where(checkpoint => checkpoint.State.IsFailed()))
            {
                run.Console.WriteLine(failed.LastError is null ? MigrationStartup.FailedState(failed) : $"{failed.CategoryId} is Failed: {failed.LastError}");

                if (run.Arguments.Retry.Contains(failed.CategoryId) && retriedFromArguments.Add(failed.CategoryId))
                {
                    run.Console.WriteLine($"Retrying {failed.CategoryId} (--retry={failed.CategoryId}).");
                    await MigrationOperatorCommands.ApplyRetry(run.CheckpointStore, run.Target, failed.CategoryId, cancellationToken);
                    changed = true;
                }
                else if (run.Arguments.Abandon.Contains(failed.CategoryId))
                {
                    run.Console.WriteLine($"Abandoning {failed.CategoryId} (--abandon={failed.CategoryId}).");

                    foreach (var line in await AbandonWarning(run, failed.CategoryId, cancellationToken))
                    {
                        run.Console.WriteLine(line);
                    }

                    await Abandon(run, failed.CategoryId, cancellationToken);
                    changed = true;
                }
                else if (!run.Arguments.NonInteractive)
                {
                    if (!await RetryOrAbandon(run, failed.CategoryId, cancellationToken))
                    {
                        run.Console.WriteLine("Stopped. Run --start-data-migration again to come back to this step.");
                        return 1;
                    }

                    changed = true;
                }
            }

            if (!changed)
            {
                MigrationStartup.RefuseIfAnyCategoryDidNotComplete(run.ToCopy, run.Finished, MigrationStartup.UnfinishedRowsOutsideTheCopy(run.CheckpointsAtStart, run.ToCopy), run.Settings);
                return null;
            }

            run.Finished = await CopyStep.CopyPass(run, cancellationToken);
        }
    }

    /// <summary>
    /// What each abandoned row kept, one line per row, naming for a dependant the category whose data it needs.
    /// The named category is the last row, as <see cref="MigrationOperatorCommands.ApplyAbandon" /> returns them.
    /// </summary>
    public static IReadOnlyList<string> AbandonReport(IReadOnlyList<MigrationCheckpoint> abandoned)
    {
        var named = abandoned[^1];

        return [.. abandoned.Select(row =>
        {
            var why = row == named ? "" : $", because it needs the data of {named.CategoryId}";
            return $"{row.CategoryId} is abandoned{why}, with {row.CopiedCount} rows copied and {row.SkippedCount} skipped{MigrationStartup.AlreadyInTheTarget(row, "and")}. Whatever it had not copied stays only in {PersistenceFactory.MigrationSourcePersistenceType}.";
        })];
    }

    // Returns false when the operator chose to stop.
    static async Task<bool> RetryOrAbandon(MigrationRun run, string categoryId, CancellationToken cancellationToken)
    {
        while (true)
        {
            run.Console.WriteLine("[r] Retry now.  [a] Abandon.  [s] Stop.");

            switch (run.Console.Choose("Choice:", "ras", cancellationToken))
            {
                case 's':
                    return false;
                case 'r':
                    try
                    {
                        await MigrationOperatorCommands.ApplyRetry(run.CheckpointStore, run.Target, categoryId, cancellationToken);
                        return true;
                    }
                    // The target refuses a retry with this type, for example once ServiceControl has opened on it.
                    catch (InvalidOperationException exception)
                    {
                        run.LoggerFactory.CreateLogger<FailedCategoriesStep>().LogWarning(exception, "The target refused to retry {CategoryId}", categoryId);
                        run.Console.WriteLine(MigrationCommandHost.ErrorText(exception, "--start-data-migration"));
                    }

                    break;
                default:
                    if (AbandonConfirmation.Ask(run.Console, await AbandonWarning(run, categoryId, cancellationToken), cancellationToken))
                    {
                        await Abandon(run, categoryId, cancellationToken);
                        return true;
                    }

                    break;
            }
        }
    }

    static Task<IReadOnlyList<string>> AbandonWarning(MigrationRun run, string categoryId, CancellationToken cancellationToken) =>
        WithDependants(run, categoryId,
        [
            $"What {categoryId} has not copied stays only in {PersistenceFactory.MigrationSourcePersistenceType}.",
            $"Once ServiceControl starts on {run.Settings.PersistenceType} it cannot be copied later."
        ], cancellationToken);

    /// <summary>
    /// The abandon warning, followed by a line naming the unfinished categories the abandon also takes because they need its data.
    /// Abandoned is final, so the operator sees them before typing ABANDON. A finished one is not named, because the abandon leaves it alone.
    /// </summary>
    public static async Task<IReadOnlyList<string>> WithDependants(MigrationRun run, string categoryId, IReadOnlyList<string> whatIsLost, CancellationToken cancellationToken = default)
    {
        var warning = new List<string>(whatIsLost);

        var dependants = new List<string>();

        foreach (var dependantId in MigrationOperatorCommands.CategoriesThatNeedItsData(categoryId, MigrationCategoryRegistry.All))
        {
            if ((await run.CheckpointStore.Read(dependantId, cancellationToken))?.State.IsFinished() != true)
            {
                dependants.Add(dependantId);
            }
        }

        if (dependants.Count > 0)
        {
            warning.Add($"Abandoning {categoryId} also abandons {string.Join(", ", dependants)}, because {(dependants.Count == 1 ? "it needs" : "they need")} its data.");
        }

        return warning;
    }

    /// <summary>
    /// Abandons the category and the unfinished ones that need its data, and prints what each one kept.
    /// </summary>
    public static async Task Abandon(MigrationRun run, string categoryId, CancellationToken cancellationToken = default)
    {
        var abandoned = await MigrationOperatorCommands.ApplyAbandon(run.CheckpointStore, categoryId, run.TimeProvider.GetUtcNow().UtcDateTime, run.LoggerFactory.CreateLogger<FailedCategoriesStep>(), cancellationToken);

        foreach (var line in AbandonReport(abandoned))
        {
            run.Console.WriteLine(line);
        }
    }
}

/// <summary>
/// Asks the operator to type ABANDON before anything is abandoned, after printing what the abandon gives up.
/// </summary>
static class AbandonConfirmation
{
    public static bool Ask(MigrationConsole console, IReadOnlyList<string> warning, CancellationToken cancellationToken = default)
    {
        foreach (var line in warning)
        {
            console.WriteLine(line);
        }

        return console.ConfirmTyped("Type ABANDON to confirm:", "ABANDON", cancellationToken);
    }
}
