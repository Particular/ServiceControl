namespace ServiceControl.Migration.Guided;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Compares the source's row counts with the rows the copy read, category by category.
/// After a copy it uses the counts the rows step took instead of counting again.
/// </summary>
sealed class VerifyStep : IMigrationStep
{
    public string Title => "Verify";

    public bool AppliesTo(MigrationRun run) => true;

    public async Task<int?> Run(MigrationRun run, CancellationToken cancellationToken = default)
    {
        if (run.SourceUnavailable)
        {
            run.Console.WriteLine($"Verify skipped: the {PersistenceFactory.MigrationSourcePersistenceType} source could not be opened, so nothing could be counted. The Checks step above shows why.");
            run.ExitCode = 2;
            return null;
        }

        var checkpoints = await run.CheckpointStore.ReadAll(cancellationToken);
        run.ExitCode = await Report(run.Console, checkpoints, run.Source, run.Counts, cancellationToken);
        return null;
    }

    /// <summary>
    /// Prints the verification report for the required categories and returns the exit code: 0 clean, 1 a category is INCOMPLETE, 2 rows were not read.
    /// A count in <paramref name="knownCounts" /> is used instead of counting that category again.
    /// The optional categories are left out, because ServicePulse starts and shows them.
    /// </summary>
    public static async Task<int> Report(MigrationConsole console, IReadOnlyList<MigrationCheckpoint> checkpoints, IMigrationSource source, IReadOnlyDictionary<string, long> knownCounts, CancellationToken cancellationToken = default)
    {
        var byId = checkpoints.ToDictionary(checkpoint => checkpoint.CategoryId);
        var results = new List<MigrationVerificationResult>();

        foreach (var category in MigrationCategoryRegistry.WithUnknownRows(checkpoints).Where(category => category.Kind == MigrationCategoryKind.Required))
        {
            // Count throws for a category the source cannot read.
            long? sourceCount = knownCounts.TryGetValue(category.Id, out var known)
                ? known
                : source.SupportedCategoryIds.Contains(category.Id) ? await source.Count(category, cancellationToken) : null;

            results.Add(MigrationVerification.Evaluate(category.Id, selected: true, sourceCount, byId.GetValueOrDefault(category.Id)));
        }

        console.WriteLine("ServiceControl migration verification");
        console.WriteLine();
        console.WriteLine($"{"Category",-40}{"Source",12}{"Copied",12}{"Skipped",10}{"Present",10}{"Unaccounted",13}  Verdict");

        foreach (var result in results)
        {
            console.WriteLine($"{result.CategoryId,-40}{Counted(result.SourceCount),12}{result.CopiedCount,12:N0}{result.SkippedCount,10:N0}{result.AlreadyPresentCount,10:N0}{Counted(result.Unaccounted),13}  {Verdict(result.Verdict)}");
        }

        PrintSkipReasons(console, results);
        PrintAbandonedLoss(console, results, PersistenceFactory.MigrationSourcePersistenceType);
        PrintRowsNotRead(console, results, PersistenceFactory.MigrationSourcePersistenceType);

        var faults = results.Count(result => result.IsFault);
        var rowsNotRead = results.Any(result => result.Verdict == MigrationVerificationVerdict.RowsNotRead);

        console.WriteLine();

        if (faults == 0 && rowsNotRead)
        {
            console.WriteLine("Every required category is Done or Abandoned, but rows were not read.");
            console.WriteLine("Read the report above before you decommission the source.");
            return 2;
        }

        if (faults == 0)
        {
            console.WriteLine("Every required category is Done or Abandoned.");
            return 0;
        }

        console.WriteLine($"{faults} categor{(faults == 1 ? "y is" : "ies are")} INCOMPLETE: Failed, still copying, or not started.");
        console.WriteLine("--migration-status shows each Failed one.");
        console.WriteLine("Each copy lists the ids of the rows it skipped as faults when it ends. Do not decommission the source.");
        return 1;
    }

    static void PrintSkipReasons(MigrationConsole console, IReadOnlyList<MigrationVerificationResult> results)
    {
        var withSkips = results.Where(result => result.SkipReasons.Count > 0).ToArray();

        if (withSkips.Length == 0)
        {
            return;
        }

        console.WriteLine();
        console.WriteLine("Skipped rows by reason. Every reason below is documented in the migration overview:");

        foreach (var result in withSkips)
        {
            foreach (var reason in result.SkipReasons.OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal))
            {
                console.WriteLine($"  {result.CategoryId,-40}{reason.Key,-32}{reason.Value,10:N0}{reason.Key.RetryNote()}");
            }
        }
    }

    static void PrintAbandonedLoss(MigrationConsole console, IReadOnlyList<MigrationVerificationResult> results, string sourcePersistenceType)
    {
        var abandoned = results.Where(result => result.Verdict == MigrationVerificationVerdict.Abandoned).ToArray();

        if (abandoned.Length == 0)
        {
            return;
        }

        console.WriteLine();
        console.WriteLine($"Abandoned categories: rows the last pass never read, which exist only in {sourcePersistenceType}.");
        console.WriteLine("A pass before a retry may have copied more, so this can overstate the loss:");

        foreach (var result in abandoned)
        {
            console.WriteLine($"  {result.CategoryId,-40}{Counted(result.Unaccounted is { } unaccounted ? Math.Max(unaccounted, 0) : null),10}");
        }
    }

    static void PrintRowsNotRead(MigrationConsole console, IReadOnlyList<MigrationVerificationResult> results, string sourcePersistenceType)
    {
        var notRead = results.Where(result => result.Verdict == MigrationVerificationVerdict.RowsNotRead).ToArray();

        if (notRead.Length == 0)
        {
            return;
        }

        console.WriteLine();
        console.WriteLine($"These categories are Done, but {sourcePersistenceType} holds rows the copy never read. These rows are not");
        console.WriteLine($"in the target, and no command copies a Done category again. Keep {sourcePersistenceType}, which is the only place they exist:");

        foreach (var result in notRead)
        {
            console.WriteLine($"  {result.CategoryId,-40}{result.Unaccounted,10:N0}");
        }
    }

    // A dash, not 0, so a category nobody counted does not look like one whose source holds nothing.
    static string Counted(long? value) => value is { } number ? number.ToString("N0") : "-";

    static string Verdict(MigrationVerificationVerdict verdict) => verdict switch
    {
        MigrationVerificationVerdict.NotSelected => "not selected",
        MigrationVerificationVerdict.Ok => "OK",
        MigrationVerificationVerdict.Abandoned => "ABANDONED, loss accepted",
        MigrationVerificationVerdict.NotCounted => "NOT COUNTED, this source cannot read it",
        MigrationVerificationVerdict.RowsNotRead => "ROWS NOT READ",
        MigrationVerificationVerdict.Incomplete => "INCOMPLETE",
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "A verdict this report has no text for.")
    };
}
