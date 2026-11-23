namespace ServiceControl.Migration.Guided;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceControl.Migration.Checks;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Runs every startup check, opens the target and the source, and reads the checkpoint table to decide
/// whether this is a first run, a resume or a settled run. A failed check throws.
/// </summary>
sealed class ChecksStep : IMigrationStep
{
    public string Title => "Checks";

    public bool AppliesTo(MigrationRun run) => true;

    public async Task<int?> Run(MigrationRun run, CancellationToken cancellationToken = default)
    {
        var sourceOpens = MigrationStartup.SourceOpens(run.Source);

        await RunInOrder(run,
        [
            new MigrationPairIsSupportedCheck(run.Settings),
            new RetryHistoryDepthIsSafeCheck(run.Settings.RetryHistoryDepth),
            .. run.Readiness.ContributedChecks(),
            new MigrationStartup.Step("the migration target opens", run.Target.Open)
        ], [sourceOpens.Name], cancellationToken);

        run.CheckpointsAtStart = await run.CheckpointStore.ReadAll(cancellationToken);

        run.Engine = new MigrationEngine(run.Source, run.Target, run.CheckpointStore, run.TimeProvider, run.Options, run.LoggerFactory.CreateLogger<MigrationEngine>());

        var copyable = MigrationStartup.CopyableCategoryIds(run.Source.SupportedCategoryIds, run.Target.SupportedCategoryIds);
        run.ToCopy = [.. run.Engine.SelectCategories(MigrationCategoryKind.Required).Where(category => copyable.Contains(category.Id))];

        run.State = MigrationStartup.RequiredCopyIsSettled(run.CheckpointsAtStart, run.ToCopy)
            ? MigrationRunState.RequiredSettled
            : run.CheckpointsAtStart.Count == 0 ? MigrationRunState.FirstRun : MigrationRunState.Resume;

        try
        {
            await MigrationStartupCheckRunner.Run([sourceOpens], cancellationToken);
            WriteOk(run, sourceOpens);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // Safe to go on without the source, because every required category is settled and only the background copy of the optional ones reads it.
        catch (Exception exception) when (run.State == MigrationRunState.RequiredSettled)
        {
            // The runner's wrapper describes a refused run, which this is not.
            var cause = exception.InnerException ?? exception;

            run.Console.WriteLine($"   [skip] {sourceOpens.Name} (could not open: {cause.Message})");

            run.LoggerFactory.CreateLogger(typeof(MigrationStartup)).LogError(cause,
                "The {SourcePersistenceType} migration source could not be opened. Every required category this build copies is Done or Abandoned and no unknown required row is unfinished, so the run goes on without it. Verify is skipped because nothing can be counted.",
                PersistenceFactory.MigrationSourcePersistenceType);

            run.SourceUnavailable = true;
            return null;
        }
        catch (Exception exception)
        {
            run.Console.WriteLine($"   [FAIL] {sourceOpens.Name}");
            throw new Exception($"{exception.Message} {MigrationStartup.NoWayOutWithoutTheSource(run.Settings)}", exception.InnerException);
        }

        await RunInOrder(run, run.Source.ContributedChecks(), [], cancellationToken);

        return null;
    }

    static async Task RunInOrder(MigrationRun run, IReadOnlyList<IMigrationStartupCheck> checks, IEnumerable<string> skippedIfOneFails, CancellationToken cancellationToken)
    {
        for (var i = 0; i < checks.Count; i++)
        {
            try
            {
                await MigrationStartupCheckRunner.Run([checks[i]], cancellationToken);
                WriteOk(run, checks[i]);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                run.Console.WriteLine($"   [FAIL] {checks[i].Name}");

                foreach (var name in checks.Skip(i + 1).Select(check => check.Name).Concat(skippedIfOneFails))
                {
                    run.Console.WriteLine($"   [skip] {name}");
                }

                throw;
            }
        }
    }

    static void WriteOk(MigrationRun run, IMigrationStartupCheck check) =>
        run.Console.WriteLine(check.CheckedAgainst is { } checkedAgainst ? $"   [ok]   {check.Name} ({checkedAgainst})" : $"   [ok]   {check.Name}");
}
