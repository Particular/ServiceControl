namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Copies one category at a time from the source to the target, and records where it got to after every batch.
/// The engine knows nothing about either database: what a row is, how it is written and how big a batch can be
/// all come from the source and the target. A category that fails comes back as a halted checkpoint rather than
/// an exception. A shutdown and a checkpoint conflict are the two things that do come out as exceptions, because
/// neither is the category's fault.
/// </summary>
public sealed class MigrationEngine(
    IMigrationSource source,
    IMigrationTarget target,
    IMigrationCheckpointStore checkpointStore,
    TimeProvider timeProvider,
    MigrationEngineOptions options,
    ILogger<MigrationEngine> logger)
{
    /// <summary>How many times a message body is read before the row is skipped as unreadable.</summary>
    public const int MaxBodyReadAttempts = 3;

    /// <summary>
    /// The categories of one kind to copy, in the order to copy them. Optional categories the operator did not
    /// ask for are left out.
    /// </summary>
    public IReadOnlyList<MigrationCategory> SelectCategories(MigrationCategoryKind kind) =>
        MigrationCategoryRegistry.All
            .Where(c => c.Kind == kind)
            .Where(c => kind == MigrationCategoryKind.Required || options.SelectedOptionalCategoryIds.Contains(c.Id))
            .OrderBy(c => c.Order)
            .ToArray();

    /// <summary>
    /// Copies the categories one after another and returns where each one ended, in the same order. A category
    /// that halts does not stop the ones after it.
    /// </summary>
    // Runs in the order given without re-sorting: required and optional orders both start at 1, so
    // sorting a mixed list would put an optional category in front of a required one.
    public async Task<IReadOnlyList<MigrationCheckpoint>> RunCategories(
        IReadOnlyList<MigrationCategory> categories,
        CancellationToken cancellationToken = default)
    {
        var results = new List<MigrationCheckpoint>(categories.Count);

        foreach (var category in categories)
        {
            results.Add(await RunCategoryAsync(category, cancellationToken));
        }

        return results;
    }

    /// <summary>
    /// Copies one category, carrying on from its saved cursor, and returns the checkpoint it ended on. A category
    /// already finished is returned untouched without reading the source.
    /// </summary>
    /// <returns>The stored checkpoint, whose state says how it ended and whose LastError says why it halted.</returns>
    /// <exception cref="MigrationCheckpointConflictException">Another writer saved this category's checkpoint, which means a second instance is copying into the same database. The state is left as that writer set it.</exception>
    /// <exception cref="OperationCanceledException">The host is shutting down. The last committed batch saved its own counts, so the stored checkpoint is already correct and a restart carries on from it.</exception>
    public async Task<MigrationCheckpoint> RunCategoryAsync(MigrationCategory category, CancellationToken cancellationToken = default)
    {
        var checkpoint = await checkpointStore.Read(category.Id, cancellationToken) ?? NotStarted(category);

        if (checkpoint.State.IsFinished())
        {
            return checkpoint;
        }

        if (category.MustFollow is { } mustFollowId)
        {
            var predecessor = await checkpointStore.Read(mustFollowId, cancellationToken);
            if (predecessor?.State.IsFinished() != true)
            {
                var predecessorState = predecessor?.State.ToString() ?? "not started";
                var blocked = checkpoint with
                {
                    State = MigrationCategoryState.Blocked,
                    LastError = $"Blocked: {category.Id} must follow {mustFollowId}, which is {predecessorState}"
                };

                logger.LogWarning("Category {CategoryId} did not run: it must follow {PredecessorId}, which is {PredecessorState}",
                    category.Id, mustFollowId, predecessorState);

                return await checkpointStore.Upsert(blocked, cancellationToken);
            }
        }

        if (checkpoint.State is MigrationCategoryState.NotStarted or MigrationCategoryState.Halted or MigrationCategoryState.Blocked)
        {
            checkpoint = await checkpointStore.Upsert(checkpoint with
            {
                State = MigrationCategoryState.InProgress,
                StartedAt = checkpoint.StartedAt ?? timeProvider.GetUtcNow().UtcDateTime,
                LastProgressAt = timeProvider.GetUtcNow().UtcDateTime,
                SettledAt = null,
                LastError = null
            }, cancellationToken);
        }

        var isFirstBatch = true;
        // Per run, not the persisted totals: the skips that tripped a halt stay on the row, so
        // counting them again would re-halt a restart whose cause has been fixed.
        var skippedThisRun = 0L;
        var processedThisRun = 0L;

        try
        {
            var batchSize = target.BatchSizeFor(category);

            // Captured once so a restart keeps the total its first start saw, and saved straight away so a run
            // killed during the first batch does not walk the whole category again to recount it.
            if (checkpoint.SourceTotal is null)
            {
                var countedRows = await source.Count(category, cancellationToken);

                checkpoint = await checkpointStore.Upsert(checkpoint with
                {
                    SourceTotal = countedRows,
                    LastProgressAt = timeProvider.GetUtcNow().UtcDateTime
                }, cancellationToken);
            }

            await foreach (var batch in source.Read(category, checkpoint.Cursor, batchSize, cancellationToken).WithCancellation(cancellationToken))
            {
                if (!isFirstBatch && category.Kind == MigrationCategoryKind.Optional)
                {
                    await Pause(options.ThrottlePause, cancellationToken);
                }
                isFirstBatch = false;

                var batchToWrite = batch;
                // Kept out of checkpoint until the write commits: the catch below saves checkpoint, and a
                // restart re-reads the uncommitted batch and would count these skips twice.
                var bodySkips = 0;
                if (category.CarriesBodies)
                {
                    var (withBodies, failed) = await FetchBodiesWithRetry(category, batch, cancellationToken);
                    batchToWrite = withBodies;
                    bodySkips = failed.Count;

                    foreach (var (id, lastAttemptError) in failed)
                    {
                        logger.LogWarning(lastAttemptError, "Skipped {SourceId} in category {CategoryId}: body unreadable after {MaxAttempts} attempts", id, category.Id, MaxBodyReadAttempts);
                    }
                }

                // The target adds its own outcome inside the transaction that writes the rows,
                // so nothing provisional is ever stored.
                var checkpointToExtend = checkpoint with
                {
                    Cursor = batch.Cursor,
                    LastProgressAt = timeProvider.GetUtcNow().UtcDateTime,
                    SkippedCount = checkpoint.SkippedCount + bodySkips,
                    SkipReasons = MigrationCheckpoint.AddSkipReasons(checkpoint.SkipReasons, bodySkips == 0 ? null : new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = bodySkips })
                };

                var result = await target.Write(category, batchToWrite, checkpointToExtend, cancellationToken);

                // The target has committed this row, so the engine takes it before anything below can throw. A halt
                // settling from the older version would be refused by the store as a conflict and lose its reason.
                checkpoint = result.Saved;

                // The result states the batch's outcome twice, as its own counts and as deltas on the checkpoint
                // it committed. The halt threshold reads the first and the end-of-run reconciliation the second.
                var committed = (result.Saved.CopiedCount - checkpointToExtend.CopiedCount, result.Saved.SkippedCount - checkpointToExtend.SkippedCount, result.Saved.AlreadyPresentCount - checkpointToExtend.AlreadyPresentCount);
                if (committed != (result.Copied, result.Skipped, result.AlreadyPresent))
                {
                    throw new InvalidOperationException($"The target reported copying {result.Copied}, skipping {result.Skipped} and finding {result.AlreadyPresent} already present in category {category.Id}, but the checkpoint it committed moved by {committed}. The halt threshold judges the first and the end-of-run reconciliation the second, so they cannot differ.");
                }

                foreach (var id in result.SkippedIds)
                {
                    logger.LogWarning("Skipped {SourceId} in category {CategoryId}", id, category.Id);
                }

                // A negative fault count would silently disarm the halt threshold for the rest of the run.
                if (result.BenignSkipped > result.Skipped)
                {
                    throw new InvalidOperationException($"The target reported {result.BenignSkipped} benign skips in category {category.Id} out of {result.Skipped} skipped rows. Benign skips are a subset of the skipped rows.");
                }

                processedThisRun += bodySkips + result.Copied + result.Skipped + result.AlreadyPresent;
                skippedThisRun += bodySkips + result.Skipped - result.BenignSkipped;

                if (HaltThreshold.Exceeded(skippedThisRun, processedThisRun, options.HaltThresholdPercent, options.HaltThresholdMinimum))
                {
                    var reason = $"Halted: {skippedThisRun} of {processedThisRun} rows skipped in this run exceeds the configured threshold of {options.HaltThresholdPercent}% and {options.HaltThresholdMinimum} rows. Fix the cause and restart to resume from the cursor, or abandon the category to accept the loss.";
                    logger.LogError("Category {CategoryId} halted at cursor {Cursor}: {LastError}", category.Id, checkpoint.Cursor, reason);
                    return await Settle(checkpoint with { State = MigrationCategoryState.Halted, LastError = reason }, cancellationToken);
                }
            }
        }
        // A shutdown is not a halt: the last committed batch saved its counts with its own rows,
        // so the checkpoint on disk is already correct and resumable.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // Another writer holds this row, which no amount of halting resolves. Leave the state alone so their row stands.
        catch (MigrationCheckpointConflictException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var position = checkpoint.Cursor is null ? "at the start" : $"at cursor {checkpoint.Cursor}";
            var reason = $"{ex.GetType().Name} {position}: {ex.Message}";
            logger.LogError(ex, "Category {CategoryId} halted at cursor {Cursor}", category.Id, checkpoint.Cursor);
            return await Settle(checkpoint with { State = MigrationCategoryState.Halted, LastError = reason }, cancellationToken);
        }

        var accountedFor = checkpoint.CopiedCount + checkpoint.SkippedCount + checkpoint.AlreadyPresentCount;

        // The counts on the row are cumulative, so a resumed run is judged on the whole category. Only a
        // shortfall is a fault: a source that has grown since it was counted is the instance still running.
        if (checkpoint.SourceTotal is { } sourceTotal && accountedFor < sourceTotal)
        {
            var reason = $"Halted: {category.Id} reached the end of the source having accounted for {accountedFor} of the {sourceTotal} rows the source reported: {checkpoint.CopiedCount} copied, {checkpoint.SkippedCount} skipped, {checkpoint.AlreadyPresentCount} already present. A cursor that no longer matches the source is the usual cause. Fix the cause and restart, or abandon the category to accept the loss.";
            logger.LogError("Category {CategoryId} halted at cursor {Cursor}: {LastError}", category.Id, checkpoint.Cursor, reason);
            return await Settle(checkpoint with { State = MigrationCategoryState.Halted, LastError = reason }, cancellationToken);
        }

        // The per-batch threshold cannot see this: a category smaller than the halt floor never reaches the
        // floor however much of it is lost, so losing most of it reads as complete with a few errors.
        if (HaltThreshold.MostOfItWasLost(skippedThisRun, processedThisRun))
        {
            var reason = $"Halted: {category.Id} reached the end of the source having skipped {skippedThisRun} of the {processedThisRun} rows this run processed, which is most of them. A category this small never reaches the threshold of {options.HaltThresholdMinimum} rows, so losing most of it is judged on its own. Fix the cause and restart to resume from the cursor, or abandon the category to accept the loss.";
            logger.LogError("Category {CategoryId} halted at cursor {Cursor}: {LastError}", category.Id, checkpoint.Cursor, reason);
            return await Settle(checkpoint with { State = MigrationCategoryState.Halted, LastError = reason }, cancellationToken);
        }

        // A halted category resumes from a cursor already at the end of the source, so the restart it was told
        // to do reads nothing and the per-run rule above can judge nothing. Judge it on every run's totals
        // instead, or a category that lost all of its rows settles as finished the moment it is restarted.
        var lostRows = FaultSkips(checkpoint);
        if (processedThisRun == 0 && HaltThreshold.MostOfItWasLost(lostRows, accountedFor))
        {
            var reason = $"Halted: {category.Id} has accounted for {accountedFor} rows across every run, {lostRows} of them skipped as faults, which is most of them. This run reached the end of the source without reading a row, so restarting again cannot change it. Fix the cause and copy the category to a fresh target database, or abandon it to accept the loss.";
            logger.LogError("Category {CategoryId} stays halted at cursor {Cursor}: {LastError}", category.Id, checkpoint.Cursor, reason);
            return await Settle(checkpoint with { State = MigrationCategoryState.Halted, LastError = reason }, cancellationToken);
        }

        return await Settle(checkpoint with { State = checkpoint.SkippedCount > 0 ? MigrationCategoryState.CompleteWithErrors : MigrationCategoryState.Complete }, cancellationToken);
    }

    // Skips of rows the product would have removed anyway are not losses, so they cannot make a category look lost.
    static long FaultSkips(MigrationCheckpoint checkpoint) =>
        checkpoint.SkipReasons is null ? 0 : checkpoint.SkipReasons.Where(reason => !reason.Key.IsBenign()).Sum(reason => reason.Value);

    // Halts log before settling: the store shares the target's database, so a failed save would hide the cause.
    Task<MigrationCheckpoint> Settle(MigrationCheckpoint settled, CancellationToken cancellationToken) =>
        checkpointStore.Upsert(settled with { SettledAt = timeProvider.GetUtcNow().UtcDateTime }, cancellationToken);

    async Task<(MigrationBatch Batch, IReadOnlyList<(string SourceId, Exception LastAttemptError)> Failed)> FetchBodiesWithRetry(MigrationCategory category, MigrationBatch batch, CancellationToken cancellationToken)
    {
        var survivors = new List<MigrationRow>(batch.Rows.Count);
        var failed = new List<(string SourceId, Exception LastAttemptError)>();

        foreach (var row in batch.Rows)
        {
            if (row.Body is not null)
            {
                survivors.Add(row);
                continue;
            }

            MigrationBody? body = null;
            Exception? lastAttemptError = null;
            var succeeded = false;

            for (var attempt = 1; attempt <= MaxBodyReadAttempts && !succeeded; attempt++)
            {
                try
                {
                    body = await source.ReadBody(category, row.SourceId, cancellationToken);
                    succeeded = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // A shutdown is not a transient body failure. Retrying it to the attempt limit and then
                    // recording the message as permanently unreadable would lose a row to a restart.
                    throw;
                }
                catch (Exception ex) when (!IsDefect(ex))
                {
                    lastAttemptError = ex;
                    logger.LogWarning(ex, "Attempt {Attempt} to read the body for {SourceId} failed", attempt, row.SourceId);
                    if (attempt < MaxBodyReadAttempts)
                    {
                        await Pause(options.BodyRetryBackoff, cancellationToken);
                    }
                }
            }

            if (succeeded)
            {
                survivors.Add(row with { Body = body });
            }
            else
            {
                failed.Add((row.SourceId, lastAttemptError!));
            }
        }

        return (batch with { Rows = survivors }, failed);
    }

    // These fail the same way on every attempt, so retrying would only turn a code defect into skipped messages.
    static bool IsDefect(Exception exception) =>
        exception is NotSupportedException or NotImplementedException or InvalidOperationException or ArgumentException or NullReferenceException or InvalidCastException;

    // Zero means no throttling, and it must return without waiting: against a fake clock
    // nobody advances, even a zero-length wait never finishes.
    Task Pause(TimeSpan duration, CancellationToken cancellationToken) =>
        duration <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(duration, timeProvider, cancellationToken);

    static MigrationCheckpoint NotStarted(MigrationCategory category) =>
        new(category.Id, MigrationCategoryState.NotStarted, null, 0, 0, null, null, null, null, null, null);
}
