namespace ServiceControl.Migration.Checks;

using System;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Registered by a test host that needs a copy to run on a build that does not yet carry the whole migration.
/// </summary>
class AllowUnreleasedMigration;

/// <summary>
/// Refuses every copy until the whole migration has shipped. A build that can copy the required categories but
/// not yet the background copy, the end-of-migration guard or verification would still commit an instance to the
/// target with no way to finish or check the move. The last phase of the migration deletes this check.
/// </summary>
class MigrationIsReleasedCheck(AllowUnreleasedMigration allowUnreleasedMigration = null) : IMigrationStartupCheck
{
    public string Name => "this build carries the whole migration";

    public Task Run(CancellationToken cancellationToken = default)
    {
        if (allowUnreleasedMigration is null)
        {
            throw new Exception(
                $"This build of ServiceControl does not yet carry the whole migration from RavenDB, so it will not copy anything. Set {MigrationSettings.EnabledKey} back to false and start ServiceControl again.");
        }

        return Task.CompletedTask;
    }
}
