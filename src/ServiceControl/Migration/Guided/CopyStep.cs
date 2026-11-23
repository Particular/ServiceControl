namespace ServiceControl.Migration.Guided;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Abandons the categories the operator chose to abandon in step 3, then copies the required categories and throws
/// unless every one of them finished. A Failed category is left to the next step.
/// </summary>
sealed class CopyStep : IMigrationStep
{
    static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(1);

    public string Title => "Copying required data";

    public bool AppliesTo(MigrationRun run) => run.State != MigrationRunState.RequiredSettled;

    public async Task<int?> Run(MigrationRun run, CancellationToken cancellationToken = default)
    {
        Logger(run).LogInformation("Offline migration: copying {CopyCount} required categories with ServiceControl stopped", run.ToCopy.Count);

        // Seeded before copying so startup can detect a copy interrupted between categories.
        foreach (var category in run.ToCopy)
        {
            if (await run.CheckpointStore.Read(category.Id, cancellationToken) is null)
            {
                await run.CheckpointStore.Upsert(new MigrationCheckpoint(category.Id, MigrationCategoryState.NotStarted, null, 0, 0, null, null, null, null, null, null), cancellationToken);
            }
        }

        foreach (var categoryId in run.PendingAbandons)
        {
            await FailedCategoriesStep.Abandon(run, categoryId, cancellationToken);
        }

        // Saved before the copy, so the progress lines and --migration-status have a percentage from the first batch.
        foreach (var (categoryId, total) in run.Counts)
        {
            var checkpoint = await run.CheckpointStore.Read(categoryId, cancellationToken);
            await run.CheckpointStore.Upsert(checkpoint with { SourceTotal = total }, cancellationToken);
        }

        run.Finished = await CopyPass(run, cancellationToken);

        // The failed categories step refuses instead, once the operator has had the choice to retry or abandon.
        if (!run.Finished.Any(checkpoint => checkpoint.State.IsFailed()))
        {
            MigrationStartup.RefuseIfAnyCategoryDidNotComplete(run.ToCopy, run.Finished, MigrationStartup.UnfinishedRowsOutsideTheCopy(run.CheckpointsAtStart, run.ToCopy), run.Settings);
        }

        return null;
    }

    /// <summary>
    /// One pass of the required copy over every category in <see cref="MigrationRun.ToCopy" />, returning where each one ended.
    /// It shows each category's progress, then lists the rows this pass skipped.
    /// </summary>
    public static async Task<IReadOnlyList<MigrationCheckpoint>> CopyPass(MigrationRun run, CancellationToken cancellationToken = default)
    {
        var logger = Logger(run);

        // Captured before the copy so the report can tell a category this pass finished from one finished before it.
        var runStartedAt = run.TimeProvider.GetUtcNow().UtcDateTime;

        // The engine keeps every skip, merge and change of the run, and a re-copy in the failed categories step must report only its own.
        var faultsBefore = run.Engine.SkippedThisRun.Count;
        var harmlessBefore = new Dictionary<(string CategoryId, MigrationSkipReason Reason), long>(run.Engine.BenignSkipsThisRun);
        var mergedAndChangedBefore = new Dictionary<string, (long Merged, long Changed)>(run.Engine.MergedAndChangedThisRun);

        var progress = new CopyProgress(run.Console);
        var categoryIds = run.ToCopy.Select(category => category.Id).ToArray();
        var shown = 0;

        // The engine copies in order, so every category before the running one has ended.
        void Show(IReadOnlyList<MigrationCheckpoint> checkpoints)
        {
            var byId = checkpoints.ToDictionary(checkpoint => checkpoint.CategoryId);
            var running = Array.FindIndex(categoryIds, shown, id => byId.GetValueOrDefault(id)?.State == MigrationCategoryState.InProgress);

            if (running < 0)
            {
                return;
            }

            for (; shown < running; shown++)
            {
                progress.Finish(byId[categoryIds[shown]]);
            }

            progress.Update(byId[categoryIds[running]]);
        }

        async Task Poll(CancellationToken stop)
        {
            // Only the delay takes the stop, so a read in flight when the pass ends finishes before the final lines are written.
#pragma warning disable PS0021
            try
#pragma warning restore PS0021
            {
                while (true)
                {
                    await Task.Delay(ProgressInterval, run.TimeProvider, stop);

                    try
                    {
                        Show(await run.CheckpointStore.ReadAll(cancellationToken));
                    }
                    // The run is cancelled, and the copy reports that itself.
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    // Log, don't throw: the progress line must never fail a copy that is still writing rows.
                    catch (Exception exception)
                    {
                        logger.LogWarning(exception, "Reading the checkpoints to show the copy's progress failed. The copy itself is unaffected and the next read tries again.");
                    }
                }
            }
            // The pass has ended.
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }
        }

        using var stopPolling = new CancellationTokenSource();
        var polling = Poll(stopPolling.Token);

        try
        {
            IReadOnlyList<MigrationCheckpoint> finished;

            try
            {
                finished = await MigrationStartup.CopyOrExplainWhyItStopped(
                    MigrationStartup.RunRequiredCategories(run.Engine, run.ToCopy, run.CheckpointStore, run.TimeProvider, logger, cancellationToken),
                    run.Settings);
            }
            finally
            {
                await stopPolling.CancelAsync();
                await polling;
            }

            for (; shown < finished.Count; shown++)
            {
                progress.Finish(finished[shown]);
            }

            var mergedAndChangedThisPass = run.Engine.MergedAndChangedThisRun.ToDictionary(
                entry => entry.Key,
                entry => (entry.Value.Merged - mergedAndChangedBefore.GetValueOrDefault(entry.Key).Merged, entry.Value.Changed - mergedAndChangedBefore.GetValueOrDefault(entry.Key).Changed));

            MigrationStartup.ReportWhatTheCopyLeftBehind(finished, logger, runStartedAt, mergedAndChangedThisPass);

            return finished;
        }
        // Also when the copy throws, because the rows skipped before the failure are committed and listed nowhere else.
        finally
        {
            progress.EndOpenLine();

            var skipLines = SkipLines(
                run.Engine.SkippedThisRun.Skip(faultsBefore),
                run.Engine.BenignSkipsThisRun
                    .Select(entry => KeyValuePair.Create(entry.Key, entry.Value - harmlessBefore.GetValueOrDefault(entry.Key)))
                    .Where(entry => entry.Value > 0));

            if (skipLines.Count > 0)
            {
                run.Console.WriteLine();
                run.Console.WriteLine("Skipped rows");

                foreach (var line in skipLines)
                {
                    run.Console.WriteLine(line);
                }
            }
        }
    }

    /// <summary>
    /// One line for each row skipped as a fault, with its source id and reason, then one line for each category and harmless reason with its count.
    /// A harmless skip loses nothing, so it is counted and never listed by id.
    /// </summary>
    public static IReadOnlyList<string> SkipLines(
        IEnumerable<(string CategoryId, MigrationSkippedRow Row)> faults,
        IEnumerable<KeyValuePair<(string CategoryId, MigrationSkipReason Reason), long>> harmless) =>
    [
        .. faults.Select(skip => $"   {skip.CategoryId,-40}{skip.Row.SourceId}  {skip.Row.Reason}"),
        .. harmless
            .OrderBy(entry => entry.Key.CategoryId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.Reason.ToString(), StringComparer.Ordinal)
            .Select(entry => $"   {entry.Key.CategoryId,-40}{entry.Key.Reason}  {entry.Value:N0}, harmless")
    ];

    // Logged under MigrationStartup, the category name that tests and log filters already match.
    static ILogger Logger(MigrationRun run) => run.LoggerFactory.CreateLogger(typeof(MigrationStartup));
}
