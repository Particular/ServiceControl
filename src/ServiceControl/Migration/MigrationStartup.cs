namespace ServiceControl.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Migration.Checks;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// The required copy, from the checks that decide whether it can run at all to the refusal it throws when a
/// category did not finish. Everything here happens with ServiceControl closed, which is the only window in
/// which the copy can be thrown away at no cost.
/// </summary>
static class MigrationStartup
{
    // A category needs both a reader and a writer, so a half-implemented one is never attempted.
    internal static IReadOnlyCollection<string> CopyableCategoryIds(IReadOnlyCollection<string> sourceSupports, IReadOnlyCollection<string> targetSupports) =>
        [.. sourceSupports.Intersect(targetSupports, StringComparer.Ordinal)];

    /// <summary>
    /// Runs the startup checks, copies every required category this build can copy, and then seeds the
    /// <see cref="IMigrationState"/> the host reads. Throws when a check refuses or a category does not finish,
    /// with a message telling the operator what to do and how to go back; the caller must let that stop the host.
    /// Call it after the host is built and before it starts, because the copy has to finish before anything else
    /// opens on the target.
    /// </summary>
    /// <param name="services">The built host's services, which is where the target, the checkpoint store and the migration state come from.</param>
    /// <param name="settings">The instance settings, read for the source and target persistence types.</param>
    /// <param name="cancellationToken">Cancelled when the host is shutting down, which ends the copy without a refusal message.</param>
    public static async Task RunRequiredCopy(IServiceProvider services, Settings settings, CancellationToken cancellationToken = default)
    {
        await MigrationStartupCheckRunner.Run(
        [
            new MigrationPairIsSupportedCheck(settings)
        ], cancellationToken);

        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(MigrationStartup));
        var target = services.GetRequiredService<IMigrationTarget>();
        var checkpointStore = services.GetRequiredService<IMigrationCheckpointStore>();
        var timeProvider = services.GetRequiredService<TimeProvider>();

        await using var source = PersistenceFactory.CreateMigrationSource(settings);

        var copyable = CopyableCategoryIds(source.SupportedCategoryIds, target.SupportedCategoryIds);

        var options = await RunChecksAndOpen(services, source, copyable, cancellationToken);

        var engine = new MigrationEngine(
            source,
            target,
            checkpointStore,
            timeProvider,
            options,
            loggerFactory.CreateLogger<MigrationEngine>());

        var selected = engine.SelectCategories(MigrationCategoryKind.Required)
            .Concat(engine.SelectCategories(MigrationCategoryKind.Optional))
            .Select(category => category.Id)
            .ToArray();

        var toCopy = engine.SelectCategories(MigrationCategoryKind.Required)
            .Where(category => copyable.Contains(category.Id))
            .ToArray();

        var deferred = engine.SelectCategories(MigrationCategoryKind.Required)
            .Where(category => !copyable.Contains(category.Id))
            .Select(category => category.Id)
            .ToArray();

        logger.LogInformation(
            "Migration mode: copying {CopyCount} required categories before ServiceControl opens ({DeferredCount} not yet implemented: {Deferred})",
            toCopy.Length, deferred.Length, string.Join(", ", deferred));

        var attempted = toCopy.Select(category => category.Id).ToHashSet(StringComparer.Ordinal);

        // Captured before the copy so the report can tell a category this run finished from one an earlier run did.
        var runStartedAt = timeProvider.GetUtcNow().UtcDateTime;

        await using var progress = new ClosedWindowProgress(checkpointStore, timeProvider, logger, attempted, cancellationToken);

        var finished = await CopyOrExplainWhyItStopped(
            engine.RunCategories(toCopy, progress.Token),
            () => progress.StalledCategoryId,
            checkpointStore,
            attempted,
            settings,
            cancellationToken);

        ReportWhatTheCopyLeftBehind(finished, logger, runStartedAt);

        RefuseIfAnyCategoryDidNotComplete(toCopy, finished, progress.StalledCategoryId, settings);

        if (services.GetRequiredService<IMigrationState>() is CheckpointMigrationState state)
        {
            await state.Seed(selected, cancellationToken);
        }
    }

    /// <summary>
    /// Runs every startup check in the order they have to run, and opens the target and the source as two of
    /// them. The order is what the operator sees: a check that costs nothing comes before one that connects to a
    /// database, and the source's own checks run last because they need it open.
    /// </summary>
    /// <param name="copyableCategoryIds">The categories both ends can handle, which is what the check on this build's coverage is given.</param>
    /// <returns>The options read from the settings, which the coherence check parsed on its way past.</returns>
    /// <exception cref="Exception">A check refused. The message names the check and says what to do, and nothing has been copied.</exception>
    public static async Task<MigrationEngineOptions> RunChecksAndOpen(IServiceProvider services, IMigrationSource source, IReadOnlyCollection<string> copyableCategoryIds, CancellationToken cancellationToken = default)
    {
        var target = services.GetRequiredService<IMigrationTarget>();
        var readiness = services.GetRequiredService<IMigrationTargetReadiness>();
        var categories = new SelectedCategoriesAreCoherentCheck();

        await MigrationStartupCheckRunner.Run(
        [
            new EveryRequiredCategoryCanBeCopiedCheck(copyableCategoryIds, services.GetService<AllowIncompleteCategorySet>()),
            categories,
            .. readiness.ContributedChecks(),
            new Step("the migration target opens", target.Open),
            new Step("the migration source opens", source.Open)
        ], cancellationToken);

        await MigrationStartupCheckRunner.Run(source.ContributedChecks(), cancellationToken);

        return categories.Options;
    }

    /// <summary>
    /// Runs the copy and turns the two ways it can stop without finishing into a refusal the operator can act on:
    /// a category that stalled, and a second instance writing checkpoints to the same database.
    /// </summary>
    /// <param name="copy">The copy, already running. It has to have been started under the stall watchdog's token, because cancelling that token is how a stalled copy is stopped.</param>
    /// <param name="stalledCategoryId">Reads which category stalled, or null when none has. It is read after the copy stops, because the watchdog sets it while the copy is still running.</param>
    /// <param name="checkpointStore">Read after a stall, to find out how far the attempted categories got.</param>
    /// <param name="attempted">The category ids this run tried to copy. A checkpoint for any other category is left out of the result.</param>
    /// <param name="settings">Read for the persistence types the refusals name.</param>
    /// <param name="cancellationToken">The host's token. Cancelling it ends the copy as a plain cancellation with no refusal, because a shutdown is not a failure.</param>
    /// <returns>What the copy returned, or what the checkpoint store holds for the attempted categories when a stall stopped it.</returns>
    /// <exception cref="Exception">A stall whose outstanding categories could not be read back, or a checkpoint saved by another instance. Both messages say what to do and how to go back.</exception>
    /// <exception cref="OperationCanceledException">The host is shutting down.</exception>
    internal static async Task<IReadOnlyList<MigrationCheckpoint>> CopyOrExplainWhyItStopped(
        Task<IReadOnlyList<MigrationCheckpoint>> copy,
        Func<string> stalledCategoryId,
        IMigrationCheckpointStore checkpointStore,
        IReadOnlySet<string> attempted,
        Settings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await copy;
        }
        // Without this, a stalled copy just ends as a cancellation and the operator never sees the refusal telling them what to do.
        // The stall cancels the linked progress token, not the caller's, so filtering on the caller's would never match.
#pragma warning disable PS0020
        catch (OperationCanceledException) when (stalledCategoryId() is not null && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // The caller's token, because the stall has already cancelled the progress one and a read on that would fail.
                return [.. (await checkpointStore.ReadAll(cancellationToken)).Where(checkpoint => attempted.Contains(checkpoint.CategoryId))];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            // Letting this out would replace the stall with a store error and send the operator after the wrong thing.
            catch (Exception exception)
            {
                throw new Exception(
                    $"The required copy did not finish, so ServiceControl will not start and nothing has been lost. {StallExplanation(stalledCategoryId())}" +
                    $"Which categories are outstanding could not be read back, because the checkpoint store is unreachable as well: {exception.Message} " +
                    RollbackAdvice(settings), exception);
            }
        }
#pragma warning restore PS0020
        // The engine never turns a conflict into a halt, so without this the copy ends on the checkpoint store's
        // own message and none of the advice every other refusal carries.
        catch (MigrationCheckpointConflictException exception)
        {
            throw new Exception(
                $"The required copy stopped because another writer saved a migration checkpoint for this instance, which is a second ServiceControl pointed at the same {settings.PersistenceType} database. ServiceControl will not start. {exception.Message} Stop the other instance, then restart with {MigrationSettings.EnabledKey} still on; the copy resumes from its last committed batch. " +
                RollbackAdvice(settings), exception);
        }
    }

    /// <summary>
    /// Throws unless every attempted category finished. A category that reported no checkpoint at all counts as
    /// outstanding too, because nothing says how much of it was copied.
    /// </summary>
    /// <param name="stalledCategoryId">The category the watchdog stopped, or null. The refusal blames the stall only when that category is one of the outstanding ones.</param>
    /// <exception cref="Exception">A category did not finish. The message names each one with its state and counts, says how to carry on, and says how to go back.</exception>
    internal static void RefuseIfAnyCategoryDidNotComplete(
        IReadOnlyList<MigrationCategory> attempted,
        IReadOnlyList<MigrationCheckpoint> finished,
        string stalledCategoryId,
        Settings settings)
    {
        var outstanding = finished
            .Where(checkpoint => !checkpoint.State.IsFinished())
            .Select(checkpoint => (checkpoint.CategoryId, Detail: $"{checkpoint.CategoryId} is {checkpoint.State} after copying {checkpoint.CopiedCount} and skipping {checkpoint.SkippedCount}{(checkpoint.LastError is null ? "" : $": {checkpoint.LastError}")}"))
            .ToList();

        var reported = finished.Select(checkpoint => checkpoint.CategoryId).ToHashSet(StringComparer.Ordinal);

        outstanding.AddRange(attempted
            .Where(category => !reported.Contains(category.Id))
            .Select(category => (CategoryId: category.Id, Detail: $"{category.Id} reported no checkpoint at all, so whether it copied anything is unknown")));

        if (outstanding.Count == 0)
        {
            return;
        }

        var detail = string.Join("; ", outstanding.Select(item => item.Detail));
        // If the stalled category finished anyway, the stall is not why these are outstanding, so don't blame it.
        var stallExplainsIt = outstanding.Any(item => string.Equals(item.CategoryId, stalledCategoryId, StringComparison.Ordinal));

        throw new Exception(
            $"The required copy did not finish, so ServiceControl will not start and nothing has been lost. {detail}. " +
            (stallExplainsIt
                ? StallExplanation(stalledCategoryId)
                : $"Fix the cause and restart with {MigrationSettings.EnabledKey} still on; the copy resumes from its last committed batch. ") +
            RollbackAdvice(settings));
    }


    /// <summary>
    /// Logs one line per category saying what it copied and what it left behind. Skipped rows are warned about
    /// one at a time while the copy runs, and nothing else states the total or says they are never coming.
    /// </summary>
    /// <param name="runStartedAt">When this start began, which is what tells a category this run finished from one an earlier run did.</param>
    internal static void ReportWhatTheCopyLeftBehind(IReadOnlyList<MigrationCheckpoint> finished, ILogger logger, DateTime runStartedAt)
    {
        foreach (var checkpoint in finished)
        {
            // A category already finished when this run began is returned without being run, so its counts are
            // an earlier run's. Reporting them in the same words as a fresh copy reads as a second copy against
            // a target that is already serving traffic.
            if (checkpoint.SettledAt is { } settledAt && settledAt < runStartedAt)
            {
                logger.LogInformation(
                    "{CategoryId}: already finished before this start, by a run that copied {Copied} and skipped {Skipped}. This start copied nothing.",
                    checkpoint.CategoryId, checkpoint.CopiedCount, checkpoint.SkippedCount);
                continue;
            }

            if (checkpoint.SkippedCount == 0)
            {
                logger.LogInformation("{CategoryId}: {Copied} copied, {AlreadyPresent} already present, nothing skipped",
                    checkpoint.CategoryId, checkpoint.CopiedCount, checkpoint.AlreadyPresentCount);
                continue;
            }

            var reasons = checkpoint.SkipReasons is { Count: > 0 } counts
                ? string.Join(", ", counts.OrderByDescending(reason => reason.Value).Select(reason => $"{reason.Key} {reason.Value}"))
                : "no reason recorded";

            logger.LogWarning(
                "{CategoryId}: {Copied} copied, {AlreadyPresent} already present, {Skipped} skipped ({Reasons}). The skipped rows were not copied and no later run will fetch them: they stay only in the source database.",
                checkpoint.CategoryId, checkpoint.CopiedCount, checkpoint.AlreadyPresentCount, checkpoint.SkippedCount, reasons);
        }
    }

    static string StallExplanation(string stalledCategoryId) =>
        $"The copy was stopped because '{stalledCategoryId}' committed nothing for {ClosedWindowProgress.StallLimit.TotalMinutes:0.#} minutes, which is not a configurable limit: check that the source and the target are both responding rather than looking for a setting to change. ";

    static string RollbackAdvice(Settings settings) =>
        $"Nothing has opened on {settings.PersistenceType} yet, so setting {MigrationSettings.EnabledKey}=false and pointing PersistenceType back at {settings.MigrationSourcePersistenceType} discards the partial copy and returns the instance to {settings.MigrationSourcePersistenceType} with no loss.";

    /// <summary>
    /// Makes opening the target or the source look like a startup check, so a failure to connect is reported in
    /// the same words as a check that refused, and in its place in the order.
    /// </summary>
    sealed class Step(string name, Func<CancellationToken, Task> run) : IMigrationStartupCheck
    {
        public string Name => name;

        public Task Run(CancellationToken cancellationToken = default) => run(cancellationToken);
    }

    /// <summary>
    /// Logs how far each category has got while the copy runs, and stops the copy when one of them commits
    /// nothing for <see cref="StallLimit" />. Without it a copy that is waiting on a database nobody is watching
    /// holds the instance closed for as long as the operator leaves it.
    /// </summary>
    internal sealed class ClosedWindowProgress : IAsyncDisposable
    {
        // Not configurable, and not a total timeout: a deadline would kill a copy that is working.
        internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
        internal static readonly TimeSpan StallLimit = TimeSpan.FromMinutes(30);

        readonly CancellationTokenSource cancellation;
        readonly HashSet<string> attempted;
        readonly DateTime watchStartedAt;
        readonly Task polling;

        public ClosedWindowProgress(IMigrationCheckpointStore checkpointStore, TimeProvider timeProvider, ILogger logger, IReadOnlyCollection<string> attemptedCategoryIds, CancellationToken cancellationToken = default)
        {
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attempted = attemptedCategoryIds.ToHashSet(StringComparer.Ordinal);
            watchStartedAt = timeProvider.GetUtcNow().UtcDateTime;
            polling = Poll(checkpointStore, timeProvider, logger, cancellation.Token);
        }

        // The copy must run under this token, because cancelling it is how the watchdog stops a stalled copy.
        public CancellationToken Token => cancellation.Token;

        /// <summary>
        /// The category that stalled, or null when none has. The watchdog sets it before it cancels the token,
        /// so read it after the copy has stopped.
        /// </summary>
        public string StalledCategoryId { get; private set; }

        async Task Poll(IMigrationCheckpointStore checkpointStore, TimeProvider timeProvider, ILogger logger, CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(PollInterval, timeProvider);

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    try
                    {
                        // Only this run's categories, because a row left in progress by an earlier run is not a stall in this one.
                        var running = (await checkpointStore.ReadAll(cancellationToken))
                            .Where(checkpoint => checkpoint.State == MigrationCategoryState.InProgress && attempted.Contains(checkpoint.CategoryId))
                            .ToArray();

                        foreach (var checkpoint in running)
                        {
                            logger.LogInformation(
                                "{CategoryId}: {Copied} of {Total} copied, {Skipped} skipped, cursor {Cursor}",
                                checkpoint.CategoryId,
                                checkpoint.CopiedCount,
                                checkpoint.SourceTotal is { } total ? total.ToString() : "an unknown number of",
                                checkpoint.SkippedCount,
                                checkpoint.Cursor ?? "the start");
                        }

                        // A resumed row carries the previous run's stamp, so the window starts at whichever is
                        // later: that stamp, or the moment this watch began.
                        var stalled = running.FirstOrDefault(checkpoint =>
                            timeProvider.GetUtcNow().UtcDateTime
                                - (checkpoint.LastProgressAt is { } lastProgress && lastProgress > watchStartedAt ? lastProgress : watchStartedAt) > StallLimit);

                        if (stalled is not null)
                        {
                            StalledCategoryId = stalled.CategoryId;
                            logger.LogError(
                                "{CategoryId} has committed nothing for {StallLimit}, so the copy is being stopped. Every committed batch is durable and the next start resumes from the cursor.",
                                stalled.CategoryId, StallLimit);
                            await cancellation.CancelAsync();
                        }
                    }
                    // The copy finished or the host is stopping: the outer catch ends the poll.
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    // Log, don't throw: an error here would come out of DisposeAsync and hide what the copy itself failed with.
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "The stall watchdog's poll failed, so a stalled copy will not be noticed until a later poll succeeds. The copy itself is unaffected and is still running.");
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Either the copy finished and disposal cancelled the poll, or the host is shutting down.
            }
        }

        public async ValueTask DisposeAsync()
        {
            await cancellation.CancelAsync();

            try
            {
                await polling;
            }
            finally
            {
                cancellation.Dispose();
            }
        }
    }
}
