namespace ServiceControl.Migration.Guided;

using System.Threading;
using System.Threading.Tasks;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Runs the steps of the offline migration in order, numbering only the ones that apply to this run.
/// Returns the exit code of the step that ended the run, or the run's own exit code after the last step.
/// </summary>
static class GuidedMigration
{
    static readonly IMigrationStep[] Steps = [new ConfigurationStep(), new ChecksStep(), new RowsToCopyStep(), new StartTheCopyStep(), new CopyStep(), new FailedCategoriesStep(), new VerifyStep()];

    // The Particular Software logo, drawn from the Website repository's logo.svg.
    static readonly string[] Banner =
    [
        "         ..:::::.....:.",
        "     .:@@@@@@@@@@@@o:..oo:",
        "   :@@@@@o:.....::o@@@@..@@:",
        " :@@@@o.  .:::::.  .:o:. .@@@.",
        ":@@@@:  .o@@@@@@@:       :@@@@.",
        "@@@@.  .@@@@@@@@@:   :o@@@@@@@@     Particular Software",
        "@@@.   o@@@@@@@@@o   :@@@@@@@@@     ServiceControl data migration",
        ".@o    :@@@@@@@@@@:   .o@@@@@@.",
        "       .@@@@@@@@@@@o.    ....",
        "        .@@@@@@@@@@@@@o:..  ..::",
        "          :@@@@@@@@@@@@@@@@@@@@.",
        "            :o@@@@@@@@@@@@@@@:",
        "               .::oo@@@@oo:."
    ];

    public static async Task<int> Run(MigrationRun run, CancellationToken cancellationToken = default)
    {
        foreach (var line in Banner)
        {
            run.Console.WriteLine(line);
        }

        run.Console.WriteLine();

        if (!run.Console.Confirm("Stop ServiceControl and every ingestion worker. Continue?", run.Arguments.NonInteractive, cancellationToken))
        {
            return 1;
        }

        run.Options = MigrationEngineOptions.ForRequiredCopy(Settings.SettingsRootNamespace);

        var number = 0;

        foreach (var step in Steps)
        {
            if (!step.AppliesTo(run))
            {
                continue;
            }

            run.Console.WriteLine();
            run.Console.WriteLine($"{++number}. {step.Title}");

            if (await step.Run(run, cancellationToken) is { } exitCode)
            {
                return exitCode;
            }
        }

        return run.ExitCode;
    }
}
