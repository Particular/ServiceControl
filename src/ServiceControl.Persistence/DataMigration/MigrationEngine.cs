namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public sealed class MigrationEngine(
    IMigrationSource source,
    IMigrationTarget target,
    IMigrationCheckpointStore checkpointStore,
    TimeProvider timeProvider,
    MigrationEngineOptions options,
    ILogger<MigrationEngine> logger)
{
    public const int MaxBodyReadAttempts = 3;

    public IReadOnlyList<MigrationCategory> SelectCategories(MigrationCategoryKind kind) =>
        MigrationCategoryRegistry.All
            .Where(c => c.Kind == kind)
            .Where(c => kind == MigrationCategoryKind.Required || options.SelectedOptionalCategoryIds.Contains(c.Id))
            .OrderBy(c => c.Order)
            .ToArray();

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

    public async Task<MigrationCheckpoint> RunCategoryAsync(MigrationCategory category, CancellationToken cancellationToken = default)
    {
        var checkpoint = await checkpointStore.Read(category.Id, cancellationToken) ?? NotStarted(category);

        // Halted is deliberately not one of them: a halt says "stopped, and here is why", and a restart after the cause is fixed has to be able to pick it up again.
        if (checkpoint.State is MigrationCategoryState.Complete or MigrationCategoryState.CompleteWithErrors or MigrationCategoryState.Abandoned)
        {
            return checkpoint;
        }

        if (category.MustFollow is { } mustFollowId)
        {
            var predecessor = await checkpointStore.Read(mustFollowId, cancellationToken);
            if (predecessor is not { State: MigrationCategoryState.Complete or MigrationCategoryState.CompleteWithErrors or MigrationCategoryState.Abandoned })
            {
                var predecessorState = predecessor?.State.ToString() ?? "not started";
                var blocked = checkpoint with
                {
                    Selected = true,
                    LastError = $"Blocked: {category.Id} must follow {mustFollowId}, which is {predecessorState}"
                };
                await checkpointStore.Upsert(blocked, cancellationToken);
                logger.LogWarning("Category {CategoryId} did not run: it must follow {PredecessorId}, which is {PredecessorState}",
                    category.Id, mustFollowId, predecessorState);
                return blocked;
            }
        }

        if (checkpoint.State is MigrationCategoryState.NotStarted or MigrationCategoryState.Halted)
        {
            checkpoint = checkpoint with
            {
                Selected = true,
                State = MigrationCategoryState.InProgress,
                StartedAt = checkpoint.StartedAt ?? timeProvider.GetUtcNow().UtcDateTime,
                CompletedAt = null,
                LastError = null
            };
            await checkpointStore.Upsert(checkpoint, cancellationToken);
        }

        var isFirstBatch = true;
        // Per run, not the persisted totals: the skips that tripped a halt stay on the row, so
        // counting them again would re-halt a restart whose cause has been fixed.
        var skippedThisRun = 0L;
        var processedThisRun = 0L;

        try
        {
            var batchSize = target.BatchSizeFor(category);
            await foreach (var batch in source.Read(category, checkpoint.Cursor, batchSize, cancellationToken).WithCancellation(cancellationToken))
            {
                if (!isFirstBatch && category.Kind == MigrationCategoryKind.Optional)
                {
                    await Pause(options.ThrottlePause, cancellationToken);
                }
                isFirstBatch = false;

                var batchToWrite = batch;
                // Stays off checkpoint until the write commits: the catch persists checkpoint, and a restart
                // re-reads an uncommitted batch and would count these skips again.
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

                // Absolute totals counting every handed-over row as copied, persisted verbatim with the rows.
                // The real split comes back in the result and lands on the next checkpoint.
                var checkpointAfterBatch = checkpoint with
                {
                    Cursor = batch.Cursor,
                    CopiedCount = checkpoint.CopiedCount + batchToWrite.Rows.Count,
                    SkippedCount = checkpoint.SkippedCount + bodySkips,
                    SkipReasons = AddSkipReasons(checkpoint.SkipReasons, bodySkips == 0 ? null : new Dictionary<string, long> { [nameof(MigrationSkipReason.BodyUnreadable)] = bodySkips })
                };

                var result = await target.Write(category, batchToWrite, checkpointAfterBatch, cancellationToken);

                checkpoint = checkpointAfterBatch with
                {
                    CopiedCount = checkpoint.CopiedCount + result.Copied,
                    SkippedCount = checkpointAfterBatch.SkippedCount + result.Skipped,
                    AlreadyPresentCount = checkpointAfterBatch.AlreadyPresentCount + result.AlreadyPresent,
                    SkipReasons = AddSkipReasons(checkpointAfterBatch.SkipReasons, result.SkipReasons)
                };

                var explainedSkips = result.SkipReasons?.Values.Sum() ?? 0;
                if (explainedSkips != result.Skipped)
                {
                    throw new InvalidOperationException($"The target reported {result.Skipped} skipped rows in category {category.Id} but gave reasons for {explainedSkips}. Every skipped row needs a reason, or --migration-verify cannot account for it.");
                }

                foreach (var id in result.SkippedIds)
                {
                    logger.LogWarning("Skipped {SourceId} in category {CategoryId}", id, category.Id);
                }

                processedThisRun += bodySkips + result.Copied + result.Skipped + result.AlreadyPresent;
                skippedThisRun += bodySkips + result.Skipped;

                if (HaltThreshold.Exceeded(skippedThisRun, processedThisRun, options.HaltThresholdPercent, options.HaltThresholdMinimum))
                {
                    var reason = $"Halted: {skippedThisRun} of {processedThisRun} rows skipped in this run exceeds the configured threshold of {options.HaltThresholdPercent}% and {options.HaltThresholdMinimum} rows. Fix the cause and restart to resume from the cursor, or abandon the category to accept the loss.";
                    logger.LogError("Category {CategoryId} halted at cursor {Cursor}: {LastError}", category.Id, checkpoint.Cursor, reason);
                    return await Settle(checkpoint with { State = MigrationCategoryState.Halted, LastError = reason }, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The target stored the last committed batch with every row counted as copied, so save its real
            // split. The category stays InProgress and the next start resumes from the cursor.
            await checkpointStore.Upsert(checkpoint, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            var reason = $"{ex.GetType().Name} at cursor {checkpoint.Cursor ?? "the start"}: {ex.Message}";
            logger.LogError(ex, "Category {CategoryId} halted at cursor {Cursor}", category.Id, checkpoint.Cursor);
            return await Settle(checkpoint with { State = MigrationCategoryState.Halted, LastError = reason }, cancellationToken);
        }

        return await Settle(checkpoint with { State = checkpoint.SkippedCount > 0 ? MigrationCategoryState.CompleteWithErrors : MigrationCategoryState.Complete }, cancellationToken);
    }

    // Halts log before settling: the store shares the target's database, so a failed save would hide the cause.
    async Task<MigrationCheckpoint> Settle(MigrationCheckpoint settled, CancellationToken cancellationToken)
    {
        settled = settled with { CompletedAt = timeProvider.GetUtcNow().UtcDateTime };
        await checkpointStore.Upsert(settled, cancellationToken);
        return settled;
    }

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

    static IReadOnlyDictionary<string, long>? AddSkipReasons(IReadOnlyDictionary<string, long>? totals, IReadOnlyDictionary<string, long>? additions)
    {
        if (additions is not { Count: > 0 })
        {
            return totals;
        }

        Dictionary<string, long> sum = totals is null ? [] : new(totals);
        foreach (var (reason, count) in additions)
        {
            sum[reason] = sum.GetValueOrDefault(reason) + count;
        }

        return sum;
    }

    // A configured pause of zero means "do not throttle", and a timer that is never going to be
    // waited on is worse than no timer: against a fake clock nobody advances, it never completes.
    Task Pause(TimeSpan duration, CancellationToken cancellationToken) =>
        duration <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(duration, timeProvider, cancellationToken);

    static MigrationCheckpoint NotStarted(MigrationCategory category) =>
        new(category.Id, Selected: false, MigrationCategoryState.NotStarted, null, 0, 0, null, null, null, null, null, null, null);
}
