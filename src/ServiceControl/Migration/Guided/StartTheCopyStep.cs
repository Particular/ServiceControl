namespace ServiceControl.Migration.Guided;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Asks before the first write of a first run. A resume does not ask again.
/// </summary>
sealed class StartTheCopyStep : IMigrationStep
{
    public string Title => "Start the copy";

    public bool AppliesTo(MigrationRun run) => run.State == MigrationRunState.FirstRun;

    public Task<int?> Run(MigrationRun run, CancellationToken cancellationToken = default)
    {
        if (run.Console.Confirm("Start the copy?", run.Arguments.NonInteractive, cancellationToken))
        {
            return Task.FromResult<int?>(null);
        }

        run.Console.WriteLine("Nothing has been written.");
        return Task.FromResult<int?>(1);
    }
}
