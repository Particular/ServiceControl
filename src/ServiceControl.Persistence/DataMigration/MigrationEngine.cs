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
/// all come from the source and the target. A category that fails comes back as a Failed checkpoint rather than
/// an exception, and no later run copies it until the operator retries or abandons it. An optional category an
/// exception stopped comes back still copying instead, with the error on its row. A shutdown and a checkpoint
/// conflict come out as exceptions, because neither is the category's fault, and so does a failure to save the
/// checkpoint a category stopped or settled on, because then there is no row to record it on.
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
    /// that halts does not stop the ones after it. Before copying anything it saves a not-started checkpoint for
    /// every category that has none, so a copy stopped between two categories still lists the ones it never reached.
    /// </summary>
    /// <exception cref="MigrationCheckpointConflictException">Another writer saved one of these checkpoints, which means a second instance is copying into the same database.</exception>
    /// <exception cref="OperationCanceledException">The host is shutting down.</exception>
    /// <exception cref="Exception">Saving the checkpoint a category stopped or settled on failed. Its stored row is the last one saved, and the categories after it were not run.</exception>
    // Runs in the order given without re-sorting: required and optional orders both start at 1, so
    // sorting a mixed list would put an optional category in front of a required one.
    public async Task<IReadOnlyList<MigrationCheckpoint>> RunCategories(
        IReadOnlyList<MigrationCategory> categories,
        CancellationToken cancellationToken = default)
    {
        // The gates that keep a host off an unfinished copy read only the rows that exist.
        foreach (var category in categories)
        {
            if (await checkpointStore.Read(category.Id, cancellationToken) is null)
            {
                await checkpointStore.Upsert(NotStarted(category), cancellationToken);
            }
        }

        var results = new List<MigrationCheckpoint>(categories.Count);

        foreach (var category in categories)
        {
            results.Add(await RunCategoryAsync(category, cancellationToken));
        }

        return results;
    }

    /// <summary>
    /// Copies one category, carrying on from its saved cursor, and returns the checkpoint it ended on. A category
    /// already finished or Failed is returned untouched without reading the source.
    /// </summary>
    /// <returns>The stored checkpoint, whose state says how it ended and whose LastError says why it stopped.</returns>
    /// <exception cref="MigrationCheckpointConflictException">Another writer saved this category's checkpoint, which means a second instance is copying into the same database. The state is left as that writer set it.</exception>
    /// <exception cref="OperationCanceledException">The host is shutting down. The last committed batch saved its own counts, so the stored checkpoint is already correct and a restart carries on from it.</exception>
    /// <exception cref="Exception">Saving the checkpoint the category stopped or settled on failed, such as a halt the store could not write. The stored row is the last one saved, so it still reads as copying from its last committed batch.</exception>
    public async Task<MigrationCheckpoint> RunCategoryAsync(MigrationCategory category, CancellationToken cancellationToken = default)
    {
        var checkpoint = await checkpointStore.Read(category.Id, cancellationToken) ?? NotStarted(category);

        if (checkpoint.State.IsFinished() || checkpoint.State.IsFailed())
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

        // A LastError on a row still copying is cleared here, because this start is trying it again.
        if (checkpoint.State is MigrationCategoryState.NotStarted or MigrationCategoryState.Blocked || checkpoint.LastError is not null)
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
        // Per run, not the persisted totals: the skips an earlier run made stay on the row, so counting them
        // again would stop a run resumed from its cursor on its first batch.
        var skippedThisRun = 0L;
        var processedThisRun = 0L;
        var rowsReadThisRun = 0L;
        // Saved after the try rather than inside it, so a failed save is not caught below as the category's own error.
        MigrationCheckpoint? halt = null;

        try
        {
            var batchSize = await target.BatchSizeFor(category, cancellationToken);

            await foreach (var batch in source.Read(category, checkpoint.Cursor, batchSize, cancellationToken).WithCancellation(cancellationToken))
            {
                if (!isFirstBatch && category.Kind == MigrationCategoryKind.Optional)
                {
                    await Pause(options.ThrottlePause, cancellationToken);
                }
                isFirstBatch = false;
                rowsReadThisRun += batch.Rows.Count;

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

                // The target has committed this row, so every halt below settles from it. A halt settling from the
                // older version would be refused by the store as a conflict and lose its reason.
                checkpoint = result.Saved;

                // The result states the batch's outcome twice, as its own counts and as deltas on the checkpoint
                // it committed. The halt threshold reads the first and status and verify read the second.
                var committed = (result.Saved.CopiedCount - checkpointToExtend.CopiedCount, result.Saved.SkippedCount - checkpointToExtend.SkippedCount, result.Saved.AlreadyPresentCount - checkpointToExtend.AlreadyPresentCount);
                if (committed != (result.Copied, result.Skipped, result.AlreadyPresent))
                {
                    var mismatch = $"The target reported copying {result.Copied}, skipping {result.Skipped} and finding {result.AlreadyPresent} already present in category {category.Id}, but the checkpoint it committed moved by {committed}.";
                    logger.LogError("Category {CategoryId} halted at cursor {Cursor}: {LastError}", category.Id, checkpoint.Cursor, mismatch);
                    halt = checkpoint with { State = MigrationCategoryState.Halted, LastError = mismatch };
                    break;
                }

                foreach (var id in result.SkippedIds)
                {
                    logger.LogWarning("Skipped {SourceId} in category {CategoryId}", id, category.Id);
                }

                processedThisRun += bodySkips + result.Copied + result.Skipped + result.AlreadyPresent;
                skippedThisRun += bodySkips + FaultSkips(result.SkipReasons);

                if (HaltThreshold.Exceeded(skippedThisRun, processedThisRun, options.HaltThresholdPercent, options.HaltThresholdMinimum))
                {
                    var reason = $"Halted: {skippedThisRun} of {processedThisRun} rows skipped in this run exceeds the configured threshold of {options.HaltThresholdPercent}% and {options.HaltThresholdMinimum} rows.";
                    logger.LogError("Category {CategoryId} halted at cursor {Cursor}: {LastError}", category.Id, checkpoint.Cursor, reason);
                    halt = checkpoint with { State = MigrationCategoryState.Halted, LastError = reason };
                    break;
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

            // An optional category copies while ServiceControl serves, so an error there waits for the next start rather than for the operator.
            if (category.Kind == MigrationCategoryKind.Optional)
            {
                logger.LogError(ex, "Optional category {CategoryId} stopped at cursor {Cursor} and stays copying until the next start", category.Id, checkpoint.Cursor);
                return await checkpointStore.Upsert(checkpoint with { LastError = $"{reason.TrimEnd('.')}. It stays copying and the next start resumes it from the cursor." }, cancellationToken);
            }

            logger.LogError(ex, "Category {CategoryId} halted at cursor {Cursor}", category.Id, checkpoint.Cursor);
            return await Settle(checkpoint with { State = MigrationCategoryState.Halted, LastError = reason }, cancellationToken);
        }

        if (halt is not null)
        {
            return await Settle(halt, cancellationToken);
        }

        // The one check a category runs on itself, judged over this run because the counts on a resumed row cover earlier runs.
        if (rowsReadThisRun != processedThisRun)
        {
            var reason = $"Halted: {category.Id} read {rowsReadThisRun} rows in this run but the target accounted for {processedThisRun} of them as copied, skipped or already present.";
            logger.LogError("Category {CategoryId} halted at cursor {Cursor}: {LastError}", category.Id, checkpoint.Cursor, reason);
            return await Settle(checkpoint with { State = MigrationCategoryState.Halted, LastError = reason }, cancellationToken);
        }

        return await Settle(checkpoint with { State = FaultSkips(checkpoint.SkipReasons) > 0 ? MigrationCategoryState.CompleteWithErrors : MigrationCategoryState.Complete }, cancellationToken);
    }

    // Harmless skips are rows the product would have removed anyway, so they neither stop a category nor leave it Failed.
    static long FaultSkips(IReadOnlyDictionary<MigrationSkipReason, long>? skipReasons) =>
        skipReasons is null ? 0 : skipReasons.Where(reason => !reason.Key.IsBenign()).Sum(reason => reason.Value);

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
