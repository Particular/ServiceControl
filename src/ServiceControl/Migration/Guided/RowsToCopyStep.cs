namespace ServiceControl.Migration.Guided;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Counts the rows of each unfinished required category and keeps the counts for verify.
/// On a first run over <see cref="LargeCategoryLimit" /> unresolved failed messages, it warns and lets the operator stop, continue or abandon them.
/// A resume does not warn again, but still applies an --abandon for them until their copy starts.
/// </summary>
sealed class RowsToCopyStep : IMigrationStep
{
    internal const long LargeCategoryLimit = 50_000;

    public string Title => "Rows to copy";

    public bool AppliesTo(MigrationRun run) => run.State != MigrationRunState.RequiredSettled;

    public async Task<int?> Run(MigrationRun run, CancellationToken cancellationToken = default)
    {
        var finished = run.CheckpointsAtStart.Where(checkpoint => checkpoint.State.IsFinished()).Select(checkpoint => checkpoint.CategoryId).ToHashSet();

        foreach (var category in run.ToCopy.Where(category => !finished.Contains(category.Id)))
        {
            // The name goes out before the count, so a count that waits on the source shows which category it is on.
            run.Console.Write($"   {category.Id,-40}");
            long count;

            try
            {
                count = await run.Source.Count(category, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                run.Console.WriteLine();
                throw;
            }
            catch (Exception)
            {
                run.Console.WriteLine();
                throw;
            }

            run.Counts[category.Id] = count;
            run.Console.WriteLine($"{count,12:N0}");
        }

        if (!run.Counts.TryGetValue(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages, out var unresolved) || unresolved <= LargeCategoryLimit)
        {
            return null;
        }

        if (run.State == MigrationRunState.FirstRun)
        {
            return await Warn(run, unresolved, cancellationToken);
        }

        // A resume keeps the answer the first run gave, but an --abandon still applies until the copy of this category starts.
        if (run.CheckpointsAtStart.FirstOrDefault(checkpoint => checkpoint.CategoryId == MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages)?.StartedAt is null)
        {
            AbandonedByArgument(run);
        }

        return null;
    }

    static bool AbandonedByArgument(MigrationRun run)
    {
        const string categoryId = MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages;

        if (!run.Arguments.Abandon.Contains(categoryId))
        {
            return false;
        }

        run.PendingAbandons.Add(categoryId);
        run.Console.WriteLine($"{categoryId} will be abandoned when the copy starts (--abandon={categoryId}).");
        return true;
    }

    static async Task<int?> Warn(MigrationRun run, long count, CancellationToken cancellationToken)
    {
        const string categoryId = MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages;
        const string stopMeans = "Nothing has been written, and you can go back to RavenDB.";

        run.Console.WriteLine();
        run.Console.WriteLine($"   WARNING: there are {count:N0} unresolved failed messages, more than {LargeCategoryLimit:N0}.");
        run.Console.WriteLine("   Copying them makes the outage long.");
        run.Console.WriteLine("   It is usually better to archive or resolve them first:");
        run.Console.WriteLine("     1. Set PersistenceType back to RavenDB, then start ServiceControl.");
        run.Console.WriteLine("     2. In ServicePulse, archive or resolve the failed messages you do not need.");
        run.Console.WriteLine("        Archived and resolved messages are not copied during the outage.");
        run.Console.WriteLine($"     3. Stop ServiceControl, set PersistenceType back to {run.Settings.PersistenceType},");
        run.Console.WriteLine("        and run --start-data-migration again.");
        run.Console.WriteLine();

        if (AbandonedByArgument(run))
        {
            return null;
        }

        if (run.Arguments.NonInteractive)
        {
            throw new Exception($"There are {count:N0} unresolved failed messages, more than {LargeCategoryLimit:N0}, and a run without a console does not continue past that. Archive or resolve them in ServicePulse until {LargeCategoryLimit:N0} or fewer remain, or pass --abandon={categoryId}.");
        }

        while (true)
        {
            run.Console.WriteLine($"   [s] Stop now. {stopMeans}");
            run.Console.WriteLine("   [c] Continue and copy them all.");
            run.Console.WriteLine($"   [a] Abandon {categoryId}.");

            switch (run.Console.Choose("Choice:", "sca", cancellationToken))
            {
                case 's':
                    run.Console.WriteLine(stopMeans);
                    return 1;
                case 'c':
                    return null;
                default:
                    var warning = await FailedCategoriesStep.WithDependants(run, categoryId,
                    [
                        "   These messages are not copied to SQL. They stay only in RavenDB.",
                        "   Once ServiceControl starts on SQL you cannot retry, archive or view them,",
                        "   and they cannot be copied later."
                    ], cancellationToken);

                    if (AbandonConfirmation.Ask(run.Console, warning, cancellationToken))
                    {
                        run.PendingAbandons.Add(categoryId);
                        return null;
                    }

                    break;
            }
        }
    }
}
