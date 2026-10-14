namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// The rows <see cref="MigrationBodyLoader.Load" /> returns for one batch, each with its body, and the rows it left out
/// with the reason for each.
/// </summary>
sealed record MigrationBodyLoadResult(IReadOnlyList<MigrationRow> Rows, IReadOnlyList<(string SourceId, MigrationSkipReason Reason)> Skipped);

/// <summary>
/// Attaches a message body to every row of a batch that arrived without one, reading at most
/// <see cref="MaxConcurrentFetches" /> bodies from the source at a time.
/// </summary>
sealed class MigrationBodyLoader(IMigrationSource source, TimeProvider timeProvider, TimeSpan retryBackoff, ILogger logger)
{
    /// <summary>
    /// How many body reads are open against the source at once. Each body is its own round trip, so reading them one at
    /// a time makes the copy as slow as the link, while opening a whole batch at once puts a load spike on a source that
    /// may still be live.
    /// </summary>
    public const int MaxConcurrentFetches = 8;

    /// <summary>
    /// Gives every row without a body the body the source holds for it. A row that already has one keeps it and
    /// is never read. A read that fails is tried up to <see cref="MigrationEngine.MaxBodyReadAttempts" /> times,
    /// and a row whose every attempt fails is left out as <see cref="MigrationSkipReason.BodyUnreadable" />. A row
    /// the source holds no body for is read once and left out as <see cref="MigrationSkipReason.BodyMissing" />.
    /// Each skip is logged as a warning.
    /// </summary>
    public async Task<MigrationBodyLoadResult> Load(MigrationCategory category, MigrationBatch batch, CancellationToken cancellationToken = default)
    {
        using var inFlight = new SemaphoreSlim(MaxConcurrentFetches);

        var fetched = await Task.WhenAll(batch.Rows.Select(async row =>
        {
            if (row.Body is not null)
            {
                return (row.Body, LastAttemptError: null);
            }

            // The slot is held for the whole fetch, retries included, so each message uses one slot rather than one per attempt.
            await inFlight.WaitAsync(cancellationToken);

            try
            {
                return await Fetch(category, row.SourceId, cancellationToken);
            }
            finally
            {
                inFlight.Release();
            }
        }));

        var rows = new List<MigrationRow>(batch.Rows.Count);
        var skipped = new List<(string SourceId, MigrationSkipReason Reason)>();

        // Rebuilt in the batch's order, because that is the cursor's order and the fetches finish in any order.
        for (var index = 0; index < batch.Rows.Count; index++)
        {
            var row = batch.Rows[index];
            var (body, lastAttemptError) = fetched[index];

            if (body is not null)
            {
                rows.Add(row with { Body = body });
            }
            else if (lastAttemptError is null)
            {
                logger.LogWarning("Skipped {SourceId} in category {CategoryId} as {Reason}: the source has no body for it, either because it holds the message without one, which a --migration-retry meets again, or because the message expired during the copy",
                    row.SourceId, category.Id, MigrationSkipReason.BodyMissing);
                skipped.Add((row.SourceId, MigrationSkipReason.BodyMissing));
            }
            else
            {
                logger.LogWarning(lastAttemptError, "Skipped {SourceId} in category {CategoryId} as {Reason}: body unreadable after {MaxAttempts} attempts", row.SourceId, category.Id, MigrationSkipReason.BodyUnreadable, MigrationEngine.MaxBodyReadAttempts);
                skipped.Add((row.SourceId, MigrationSkipReason.BodyUnreadable));
            }
        }

        return new MigrationBodyLoadResult(rows, skipped);
    }

    // A null body with no error means the source holds no body; with an error, every attempt failed.
    async Task<(MigrationBody? Body, Exception? LastAttemptError)> Fetch(MigrationCategory category, string sourceId, CancellationToken cancellationToken)
    {
        Exception? lastAttemptError = null;

        for (var attempt = 1; attempt <= MigrationEngine.MaxBodyReadAttempts; attempt++)
        {
            try
            {
                return (await source.ReadBody(category, sourceId, cancellationToken), null);
            }
            // Only the run's own cancellation stops here; a client timeout raised as a cancellation is retried.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            // Logged here because the exception that halts the category does not say which message it was reading.
            catch (Exception ex) when (IsDefect(ex))
            {
                logger.LogError(ex, "Reading the body of {SourceId} in category {CategoryId} failed in a way no retry fixes, so the batch fails", sourceId, category.Id);
                throw;
            }
            catch (Exception ex)
            {
                lastAttemptError = ex;
                logger.LogWarning(ex, "Reading the body of {SourceId} failed on attempt {Attempt} of {MaxAttempts}", sourceId, attempt, MigrationEngine.MaxBodyReadAttempts);

                if (attempt < MigrationEngine.MaxBodyReadAttempts && retryBackoff > TimeSpan.Zero)
                {
                    await Task.Delay(retryBackoff, timeProvider, cancellationToken);
                }
            }
        }

        return (null, lastAttemptError);
    }

    // These fail the same way on every attempt, so retrying would only turn a code defect into skipped messages.
    static bool IsDefect(Exception exception) =>
        exception is NotSupportedException or NotImplementedException or InvalidOperationException or ArgumentException or NullReferenceException or InvalidCastException;
}
