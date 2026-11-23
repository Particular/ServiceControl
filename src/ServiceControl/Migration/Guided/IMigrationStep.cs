namespace ServiceControl.Migration.Guided;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// One numbered step of the guided migration. A step that returns an exit code ends the run with it.
/// </summary>
interface IMigrationStep
{
    string Title { get; }

    bool AppliesTo(MigrationRun run);

    Task<int?> Run(MigrationRun run, CancellationToken cancellationToken = default);
}
