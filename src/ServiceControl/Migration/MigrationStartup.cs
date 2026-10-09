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
    /// Runs the startup checks, copies every required category this build can copy that is not already settled,
    /// and then seeds the <see cref="IMigrationState"/> the host reads. Once every required category this build
    /// copies is settled and no row under an unknown id is unfinished, a source that will not open is logged and
    /// recorded on the optional categories' rows instead of refused, nothing is copied, and the host still opens. Throws when a check refuses or a category does not
    /// finish, with a message telling the operator what to do and how to go back; the caller must let that stop
    /// the host. Call it after the host is built and before it starts, because the copy has to finish before
    /// anything else opens on the target.
    /// </summary>
    /// <param name="services">The built host's services, which is where the target, the checkpoint store and the migration state come from.</param>
    /// <param name="settings">The instance settings, read for the source and target persistence types.</param>
    /// <param name="cancellationToken">Cancelled when the host is shutting down, which ends the copy without a refusal message.</param>
    public static async Task RunRequiredCopy(IServiceProvider services, Settings settings, CancellationToken cancellationToken = default)
    {
        await MigrationStartupCheckRunner.Run(
        [
            new MigrationIsReleasedCheck(services.GetService<AllowUnreleasedMigration>()),
            new MigrationPairIsSupportedCheck(settings)
        ], cancellationToken);

        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(MigrationStartup));
        var target = services.GetRequiredService<IMigrationTarget>();
        var checkpointStore = services.GetRequiredService<IMigrationCheckpointStore>();
        var timeProvider = services.GetRequiredService<TimeProvider>();

        await using var source = PersistenceFactory.CreateMigrationSource(settings);

        var copyable = CopyableCategoryIds(source.SupportedCategoryIds, target.SupportedCategoryIds);

        var options = await RunChecksAndOpenTarget(services, settings, cancellationToken);

        var engine = new MigrationEngine(
            source,
            target,
            checkpointStore,
            timeProvider,
            options,
            loggerFactory.CreateLogger<MigrationEngine>());

        // Optional categories copy beside the running services, so only the required ones hold those services back.
        var holdingBack = engine.SelectCategories(MigrationCategoryKind.Required)
            .Select(category => category.Id)
            .ToArray();

        var toCopy = engine.SelectCategories(MigrationCategoryKind.Required)
            .Where(category => copyable.Contains(category.Id))
            .ToArray();

        var deferred = engine.SelectCategories(MigrationCategoryKind.Required)
            .Where(category => !copyable.Contains(category.Id))
            .Select(category => category.Id)
            .ToArray();

        var checkpoints = await checkpointStore.ReadAll(cancellationToken);
        var unfinishedOutsideTheCopy = UnfinishedRowsOutsideTheCopy(checkpoints, toCopy);
        var requiredCopySettled = RequiredCopyIsSettled(checkpoints, toCopy);

        try
        {
            await MigrationStartupCheckRunner.Run([SourceOpens(source)], cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // Safe to open without the source, because every required category is settled and only the background copy of the optional ones reads it.
        catch (Exception exception) when (requiredCopySettled)
        {
            // The runner's wrapper says ServiceControl will not start, which is false here and would land on the rows.
            var cause = exception.InnerException ?? exception;

            logger.LogError(cause,
                "The {SourcePersistenceType} migration source could not be opened, so ServiceControl opens without it: every required category this build copies is Done or Abandoned and no row under an id it does not know is unfinished, so only the background copy of the optional categories reads the source. The optional categories still copying stay as they are until a start that can open the source, and their checkpoints carry this error.",
                PersistenceFactory.MigrationSourcePersistenceType);

            await RecordSourceOutage(checkpointStore, engine.SelectCategories(MigrationCategoryKind.Optional), cause, cancellationToken);
            await SeedMigrationState(services, holdingBack, cancellationToken);
            return;
        }
        catch (Exception exception)
        {
            throw new Exception($"{exception.Message} {NoWayOutWithoutTheSource(settings)}", exception.InnerException);
        }

        await MigrationStartupCheckRunner.Run(source.ContributedChecks(), cancellationToken);

        logger.LogInformation(
            "Migration mode: copying {CopyCount} required categories before ServiceControl opens ({DeferredCount} not yet implemented: {Deferred})",
            toCopy.Length, deferred.Length, string.Join(", ", deferred));

        // Captured before the copy so the report can tell a category this run finished from one an earlier run did.
        var runStartedAt = timeProvider.GetUtcNow().UtcDateTime;

        var finished = await CopyOrExplainWhyItStopped(
            RunRequiredCategories(engine, toCopy, checkpointStore, timeProvider, logger, cancellationToken),
            settings);

        ReportWhatTheCopyLeftBehind(finished, logger, runStartedAt);

        RefuseIfAnyCategoryDidNotComplete(toCopy, finished, unfinishedOutsideTheCopy, settings);

        await SeedMigrationState(services, holdingBack, cancellationToken);
    }

    static async Task SeedMigrationState(IServiceProvider services, IReadOnlyCollection<string> holdingBack, CancellationToken cancellationToken)
    {
        if (services.GetRequiredService<IMigrationState>() is CheckpointMigrationState state)
        {
            await state.Seed(holdingBack, cancellationToken);
        }
    }

    /// <summary>
    /// Runs every startup check in the order they have to run, and opens the target and the source as two of
    /// them. The order is what the operator sees: a check that costs nothing comes before one that connects to a
    /// database, and the source's own checks run last because they need it open.
    /// </summary>
    /// <param name="settings">The instance settings, whose retention periods are the optional category windows when none is set, and whose retry history depth the copy must not be thrown away by.</param>
    /// <returns>The options read from the settings, which the window check parsed on its way past.</returns>
    /// <exception cref="Exception">A check refused. The message names the check and says what to do, and this start has copied nothing.</exception>
    public static async Task<MigrationEngineOptions> RunChecksAndOpen(IServiceProvider services, Settings settings, IMigrationSource source, CancellationToken cancellationToken = default)
    {
        var options = await RunChecksAndOpenTarget(services, settings, cancellationToken);

        await MigrationStartupCheckRunner.Run([SourceOpens(source)], cancellationToken);

        await MigrationStartupCheckRunner.Run(source.ContributedChecks(), cancellationToken);

        return options;
    }

    /// <summary>
    /// Runs every startup check that does not need the source, in the order they have to run, ending with
    /// opening the target. A check that costs nothing comes before one that connects to a database. The target
    /// is open when this returns, and the source has not been touched.
    /// </summary>
    /// <param name="services">The built host's services, which is where the target and its readiness checks come from.</param>
    /// <param name="settings">The instance settings, whose retention periods are the optional category windows when none is set, and whose retry history depth the copy must not be thrown away by.</param>
    /// <param name="cancellationToken">Cancelled when the host is shutting down, which stops the checks without a refusal message.</param>
    /// <returns>The options read from the settings, which the window check parsed on its way past.</returns>
    /// <exception cref="Exception">A check refused or the target did not open. The message names the check and says what to do, and this start has copied nothing.</exception>
    /// <exception cref="OperationCanceledException">The host is shutting down.</exception>
    public static async Task<MigrationEngineOptions> RunChecksAndOpenTarget(IServiceProvider services, Settings settings, CancellationToken cancellationToken = default)
    {
        var target = services.GetRequiredService<IMigrationTarget>();
        var readiness = services.GetRequiredService<IMigrationTargetReadiness>();
        var categories = new OptionalCategoryWindowsAreValidCheck(settings.EventsRetentionPeriod, settings.ErrorRetentionPeriod);

        await MigrationStartupCheckRunner.Run(
        [
            categories,
            new RetryHistoryDepthIsSafeCheck(settings.RetryHistoryDepth),
            .. readiness.ContributedChecks(),
            new Step("the migration target opens", target.Open)
        ], cancellationToken);

        return categories.Options;
    }

    /// <summary>
    /// Records on each optional category's checkpoint that the source could not be opened, so status and verify
    /// read the outage from the rows. The required copy calls it on a start that
    /// opens without the source, and the background copier calls it when its own open fails. It changes no
    /// state, cursor or count: a row still copying gains the error as its <see cref="MigrationCheckpoint.LastError" />,
    /// a category with no row gets a new not-started row carrying the error, and a row that is Done, Failed or
    /// Abandoned is left alone. The next start that resumes a row clears the error.
    /// </summary>
    /// <param name="checkpointStore">Where the rows are read and saved.</param>
    /// <param name="optionalCategories">The optional categories this instance selected.</param>
    /// <param name="exception">Why the source could not be opened. Its type and message go into the error.</param>
    /// <param name="cancellationToken">Cancelled when the host is shutting down.</param>
    /// <exception cref="MigrationCheckpointConflictException">Another writer saved one of these rows since it was read.</exception>
    internal static async Task RecordSourceOutage(IMigrationCheckpointStore checkpointStore, IReadOnlyList<MigrationCategory> optionalCategories, Exception exception, CancellationToken cancellationToken = default)
    {
        var error = $"The {PersistenceFactory.MigrationSourcePersistenceType} migration source could not be opened: {exception.GetType().Name}: {exception.Message.TrimEnd('.', ' ')}. This category stays as it is, and the next start tries the source again.";

        foreach (var category in optionalCategories)
        {
            var checkpoint = await checkpointStore.Read(category.Id, cancellationToken);

            if (checkpoint is null)
            {
                await checkpointStore.Upsert(new MigrationCheckpoint(category.Id, MigrationCategoryState.NotStarted, null, 0, 0, null, null, null, null, null, error), cancellationToken);
            }
            else if (!checkpoint.State.IsFinished() && !checkpoint.State.IsFailed())
            {
                await checkpointStore.Upsert(checkpoint with { LastError = error }, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Copies the required categories one after another under the stall watchdog and returns where each one ended,
    /// in the same order. A category that commits nothing for <see cref="ClosedWindowProgress.StallLimit" /> is
    /// stopped and settled Halted, and the categories after it still get their go. Before copying anything it saves
    /// a not-started checkpoint for every category that has none, as <see cref="MigrationEngine.RunCategories" /> does.
    /// </summary>
    /// <param name="engine">Copies each category.</param>
    /// <param name="categories">The categories to copy, in the order to copy them.</param>
    /// <param name="checkpointStore">Where each category's row is seeded, watched and, after a stall, settled.</param>
    /// <param name="timeProvider">The clock the watchdog measures a stall on.</param>
    /// <param name="logger">Receives the watchdog's progress lines and the stall.</param>
    /// <param name="cancellationToken">The host's token. Cancelling it stops the copy and leaves the running category as its last committed batch left it, so the next start resumes it.</param>
    /// <returns>The checkpoint each category ended on.</returns>
    /// <exception cref="MigrationCheckpointConflictException">Another writer saved one of these checkpoints, which means a second instance is copying into the same database.</exception>
    /// <exception cref="OperationCanceledException">The host is shutting down.</exception>
    internal static async Task<IReadOnlyList<MigrationCheckpoint>> RunRequiredCategories(
        MigrationEngine engine,
        IReadOnlyList<MigrationCategory> categories,
        IMigrationCheckpointStore checkpointStore,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // The gates that keep a host off an unfinished copy read only the rows that exist.
        foreach (var category in categories)
        {
            if (await checkpointStore.Read(category.Id, cancellationToken) is null)
            {
                await checkpointStore.Upsert(new MigrationCheckpoint(category.Id, MigrationCategoryState.NotStarted, null, 0, 0, null, null, null, null, null, null), cancellationToken);
            }
        }

        await using var progress = new ClosedWindowProgress(checkpointStore, timeProvider, logger, [.. categories.Select(category => category.Id)], cancellationToken);

        var results = new List<MigrationCheckpoint>(categories.Count);

        foreach (var category in categories)
        {
            using var categoryCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            progress.Watch(category.Id, timeProvider.GetUtcNow().UtcDateTime, categoryCancellation);

            try
            {
                results.Add(await engine.RunCategoryAsync(category, categoryCancellation.Token));
            }
            // Only the watchdog cancels this source without the host's token, so the filter tells a stall from a shutdown.
#pragma warning disable PS0020
            catch (OperationCanceledException) when (categoryCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
#pragma warning restore PS0020
            {
                // The host's token, because the stall has already cancelled the category's and a store call on that would fail.
                var stalled = await checkpointStore.Read(category.Id, cancellationToken);

                results.Add(await checkpointStore.Upsert(stalled with
                {
                    State = MigrationCategoryState.Halted,
                    SettledAt = timeProvider.GetUtcNow().UtcDateTime,
                    LastError = StallExplanation(category.Id)
                }, cancellationToken));
            }
        }

        return results;
    }

    /// <summary>
    /// Waits for the copy and turns a second instance writing checkpoints to the same database into a refusal the
    /// operator can act on. A shutdown and every other failure come out as they are.
    /// </summary>
    /// <param name="copy">The copy, already running.</param>
    /// <param name="settings">Read for the persistence type the refusal names.</param>
    /// <returns>What the copy returned.</returns>
    /// <exception cref="Exception">A checkpoint saved by another instance. The message says what to do and how to go back.</exception>
    /// <exception cref="OperationCanceledException">The host is shutting down.</exception>
#pragma warning disable PS0018 // The copy it waits on already runs under the host's token, so a token here would have nothing to cancel.
    internal static async Task<IReadOnlyList<MigrationCheckpoint>> CopyOrExplainWhyItStopped(
        Task<IReadOnlyList<MigrationCheckpoint>> copy,
        Settings settings)
#pragma warning restore PS0018
    {
        try
        {
            return await copy;
        }
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
    /// <exception cref="Exception">A category did not finish. The message names each one with its state and counts, names the commands that move each Failed one on, and says how to go back.</exception>
    internal static void RefuseIfAnyCategoryDidNotComplete(
        IReadOnlyList<MigrationCategory> attempted,
        IReadOnlyList<MigrationCheckpoint> finished,
        Settings settings)
    {
        var outstanding = finished
            .Where(checkpoint => !checkpoint.State.IsFinished())
            .Select(checkpoint => checkpoint.State.IsFailed() ? FailedDetail(checkpoint) : CopyingDetail(checkpoint))
            .ToList();

        var reported = finished.Select(checkpoint => checkpoint.CategoryId).ToHashSet(StringComparer.Ordinal);

        outstanding.AddRange(attempted
            .Where(category => !reported.Contains(category.Id))
            .Select(category => $"{category.Id} reported no checkpoint at all, so whether it copied anything is unknown"));

        if (outstanding.Count == 0)
        {
            return;
        }

        throw new Exception(
            $"The required copy did not finish, so ServiceControl will not start and nothing has been lost. {string.Join(". ", outstanding)}. " +
            RollbackAdvice(settings));
    }

    /// <summary>
    /// Says whether the host may open without the source: every category this start copies is finished, and no row
    /// outside the copy is unfinished.
    /// </summary>
    /// <param name="checkpoints">Every checkpoint row in the target.</param>
    /// <param name="toCopy">The categories this start copies.</param>
    /// <returns>True when nothing required is still outstanding.</returns>
    internal static bool RequiredCopyIsSettled(IReadOnlyList<MigrationCheckpoint> checkpoints, IReadOnlyList<MigrationCategory> toCopy) =>
        UnfinishedRowsOutsideTheCopy(checkpoints, toCopy).Count == 0
        && toCopy.All(category => checkpoints.Any(checkpoint => checkpoint.CategoryId == category.Id && checkpoint.State.IsFinished()));

    /// <summary>
    /// Throws unless every attempted category finished and no row outside the copy is unfinished, naming each
    /// outstanding row in the same words.
    /// </summary>
    /// <param name="attempted">The categories this start copied.</param>
    /// <param name="finished">Where each attempted category ended.</param>
    /// <param name="unfinishedOutsideTheCopy">The rows <see cref="UnfinishedRowsOutsideTheCopy" /> found.</param>
    /// <param name="settings">Read for the persistence type the refusal names.</param>
    /// <exception cref="Exception">Something is outstanding. The message names it and says how to go back.</exception>
    internal static void RefuseIfAnyCategoryDidNotComplete(
        IReadOnlyList<MigrationCategory> attempted,
        IReadOnlyList<MigrationCheckpoint> finished,
        IReadOnlyList<MigrationCheckpoint> unfinishedOutsideTheCopy,
        Settings settings) =>
        RefuseIfAnyCategoryDidNotComplete(attempted, [.. finished, .. unfinishedOutsideTheCopy], settings);

    /// <summary>
    /// Finds the unfinished rows the copy will not run: a row under an id this build does not know, which counts as
    /// required because a newer build may have written it for a category that must finish, and a required row for a
    /// category this build cannot copy yet. A finished row, and a row known to be optional, hold nothing.
    /// </summary>
    /// <param name="checkpoints">Every checkpoint row in the target.</param>
    /// <param name="toCopy">The categories this start copies, whose rows the copy judges itself.</param>
    /// <returns>The rows that keep the host closed, in the order they were read.</returns>
    internal static IReadOnlyList<MigrationCheckpoint> UnfinishedRowsOutsideTheCopy(IReadOnlyList<MigrationCheckpoint> checkpoints, IReadOnlyList<MigrationCategory> toCopy) =>
    [.. checkpoints
        .Where(checkpoint => MigrationCategoryRegistry.Find(checkpoint.CategoryId)?.Kind != MigrationCategoryKind.Optional)
        .Where(checkpoint => toCopy.All(category => category.Id != checkpoint.CategoryId))
        .Where(checkpoint => !checkpoint.State.IsFinished())];

    internal static string FailedDetail(MigrationCheckpoint checkpoint)
    {
        var reasons = checkpoint.SkipReasons is { Count: > 0 } counts
            ? $" ({string.Join("; ", counts.Select(reason => $"{reason.Key} {reason.Value}{(reason.Key.IsPermanent() ? ", no retry can fix" : "")}"))})"
            : "";

        return $"{checkpoint.CategoryId} is Failed ({checkpoint.State}) after copying {checkpoint.CopiedCount} and skipping {checkpoint.SkippedCount}{reasons}{LastErrorClause(checkpoint)}; " +
            $"run --migration-retry {checkpoint.CategoryId} once the cause is fixed, which copies the category again from the start, or --migration-abandon {checkpoint.CategoryId} to keep what was copied and give up the rest, both with ServiceControl stopped";
    }

    static string CopyingDetail(MigrationCheckpoint checkpoint) =>
        $"{checkpoint.CategoryId} is {checkpoint.State} after copying {checkpoint.CopiedCount} and skipping {checkpoint.SkippedCount}{LastErrorClause(checkpoint)}";

    // Trimmed because the refusal punctuates each category's sentence itself.
    static string LastErrorClause(MigrationCheckpoint checkpoint) =>
        checkpoint.LastError is null ? "" : $": {checkpoint.LastError.TrimEnd('.', ' ')}";

    /// <summary>
    /// Logs one line per category saying what it copied and what it left behind. Skipped rows are warned about
    /// one at a time while the copy runs, and nothing else states the total or says what becomes of them.
    /// </summary>
    /// <param name="runStartedAt">When this start began, which is what tells a category this run finished from one an earlier run did.</param>
    internal static void ReportWhatTheCopyLeftBehind(IReadOnlyList<MigrationCheckpoint> finished, ILogger logger, DateTime runStartedAt)
    {
        foreach (var checkpoint in finished)
        {
            // It did not run, and the refusal that follows names it with the category it waits for.
            if (checkpoint.State == MigrationCategoryState.Blocked)
            {
                continue;
            }

            // A category already finished when this run began is returned without being run, so its counts are
            // an earlier run's. A Failed one is returned untouched too, but it is not finished, so it falls through.
            if (checkpoint.State.IsFinished() && checkpoint.SettledAt is { } settledAt && settledAt < runStartedAt)
            {
                logger.LogInformation(
                    "{CategoryId}: already finished before this start, by a run that copied {Copied} and skipped {Skipped}. This start copied nothing.",
                    checkpoint.CategoryId, checkpoint.CopiedCount, checkpoint.SkippedCount);
                continue;
            }

            // An exception, a stall or unbalanced counts fail a category without a skip, and that is not a clean copy.
            if (checkpoint.State.IsFailed() && checkpoint.SkippedCount == 0)
            {
                logger.LogWarning("{CategoryId} is Failed ({State}) after {Copied} copied and {AlreadyPresent} already present. The refusal that follows says why and what to run.",
                    checkpoint.CategoryId, checkpoint.State, checkpoint.CopiedCount, checkpoint.AlreadyPresentCount);
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

            // Only a Failed category can be retried, so a Done one is never offered the command.
            var whatBecomesOfThem = checkpoint.State.IsFailed()
                ? WhatARetryCanDo(checkpoint)
                : checkpoint.State == MigrationCategoryState.Complete
                    ? "They were left out as harmless, because ServiceControl would have removed them anyway, and they stay only in the source database."
                    : "They stay only in the source database.";

            logger.LogWarning(
                "{CategoryId}: {Copied} copied, {AlreadyPresent} already present, {Skipped} skipped ({Reasons}). {WhatBecomesOfThem}",
                checkpoint.CategoryId, checkpoint.CopiedCount, checkpoint.AlreadyPresentCount, checkpoint.SkippedCount, reasons, whatBecomesOfThem);
        }
    }

    // A retry re-reads the whole category, so it is offered only when some of the faults could come across on it.
    static string WhatARetryCanDo(MigrationCheckpoint checkpoint)
    {
        var faults = checkpoint.SkipReasons?.Keys.Where(reason => !reason.IsBenign()).ToArray() ?? [];
        var permanent = faults.Where(reason => reason.IsPermanent()).ToArray();

        if (faults.Length > 0 && permanent.Length == faults.Length)
        {
            return $"They stay only in the source database, and no retry can fix them, so --migration-abandon {checkpoint.CategoryId} keeps what was copied and gives up the rest.";
        }

        var cannotFix = permanent.Length == 0 ? "" : $" No retry can fix the {string.Join(" or ", permanent)} ones.";

        return $"They stay only in the source database until --migration-retry {checkpoint.CategoryId} re-reads them once the cause is fixed.{cannotFix}";
    }

    internal static string StallExplanation(string stalledCategoryId) =>
        $"Halted: {stalledCategoryId} committed nothing for {ClosedWindowProgress.StallLimit.TotalMinutes:0.#} minutes, so it was stopped. The limit is not configurable: check that the source and the target are both responding rather than looking for a setting to change.";

    static string RollbackAdvice(Settings settings) =>
        $"Nothing has opened on {settings.PersistenceType} yet, so setting {MigrationSettings.EnabledKey}=false and pointing PersistenceType back at {PersistenceFactory.MigrationSourcePersistenceType} discards the partial copy and returns the instance to {PersistenceFactory.MigrationSourcePersistenceType} with no loss.";

    static string NoWayOutWithoutTheSource(Settings settings) =>
        $"If the {PersistenceFactory.MigrationSourcePersistenceType} source is already gone, a required category that is Failed or has started copying can be given up with --migration-abandon <category>, with ServiceControl stopped. " +
        $"A required category that never started cannot be abandoned: while one is outstanding ServiceControl has never opened on {settings.PersistenceType}, so pointing PersistenceType back at {PersistenceFactory.MigrationSourcePersistenceType}, or starting over against an empty {settings.PersistenceType} database, loses nothing it has served.";

    static Step SourceOpens(IMigrationSource source) => new("the migration source opens", source.Open);

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
    /// Logs how far each category has got while the copy runs, and stops the running category when it commits
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
        readonly Task polling;
        volatile RunningCategory running;

        public ClosedWindowProgress(IMigrationCheckpointStore checkpointStore, TimeProvider timeProvider, ILogger logger, IReadOnlyCollection<string> attemptedCategoryIds, CancellationToken cancellationToken = default)
        {
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attempted = attemptedCategoryIds.ToHashSet(StringComparer.Ordinal);
            polling = Poll(checkpointStore, timeProvider, logger, cancellation.Token);
        }

        /// <summary>
        /// Points the watchdog at the category that is starting now, in place of the one before it. Only this
        /// category is judged from here on, from the later of its last committed batch and <paramref name="runStartedAt" />.
        /// </summary>
        /// <param name="categoryId">The category starting now.</param>
        /// <param name="runStartedAt">When this start began running it. A row an earlier start left carries an older stamp, which would read as a stall at once.</param>
        /// <param name="stop">Cancelled when the category stalls. The category must run under its token, and nothing else should.</param>
        public void Watch(string categoryId, DateTime runStartedAt, CancellationTokenSource stop) =>
            running = new RunningCategory(categoryId, runStartedAt, stop);

        async Task Poll(IMigrationCheckpointStore checkpointStore, TimeProvider timeProvider, ILogger logger, CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(PollInterval, timeProvider);

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    try
                    {
                        // Only this run's categories, because a row an earlier run left in progress is not this run's to report.
                        var inProgress = (await checkpointStore.ReadAll(cancellationToken))
                            .Where(checkpoint => checkpoint.State == MigrationCategoryState.InProgress && attempted.Contains(checkpoint.CategoryId))
                            .ToArray();

                        foreach (var checkpoint in inProgress)
                        {
                            logger.LogInformation(
                                "{CategoryId}: {Copied} of {Total} copied, {Skipped} skipped, cursor {Cursor}",
                                checkpoint.CategoryId,
                                checkpoint.CopiedCount,
                                checkpoint.SourceTotal is { } total ? total.ToString() : "an unknown number of",
                                checkpoint.SkippedCount,
                                checkpoint.Cursor ?? "the start");
                        }

                        var watched = running;

                        // A resumed row carries the previous run's stamp, so the window starts at whichever is
                        // later: that stamp, or the moment this category's run began.
                        var stalled = watched is not null && !watched.Stop.IsCancellationRequested && inProgress.Any(checkpoint =>
                            checkpoint.CategoryId == watched.CategoryId
                            && timeProvider.GetUtcNow().UtcDateTime
                                - (checkpoint.LastProgressAt is { } lastProgress && lastProgress > watched.RunStartedAt ? lastProgress : watched.RunStartedAt) > StallLimit);

                        if (stalled)
                        {
                            logger.LogError(
                                "{CategoryId} has committed nothing for {StallLimit}, so it is being stopped and settled Halted. The categories after it still get their go.",
                                watched.CategoryId, StallLimit);
                            await watched.Stop.CancelAsync();
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

        sealed record RunningCategory(string CategoryId, DateTime RunStartedAt, CancellationTokenSource Stop);
    }
}
