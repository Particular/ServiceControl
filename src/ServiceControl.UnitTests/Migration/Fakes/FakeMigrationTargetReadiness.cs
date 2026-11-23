namespace ServiceControl.UnitTests.Migration.Fakes;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

public sealed class FakeMigrationTargetReadiness : IMigrationTargetReadiness
{
    public List<IMigrationStartupCheck> Checks { get; } = [];

    /// <summary>
    /// How many times the run asked for the target's checks.
    /// </summary>
    public int ChecksRequested { get; private set; }

    public bool HostOpened { get; private set; }

    public IReadOnlyList<MigrationSettingValue> Configuration { get; set; } = [];

    public IReadOnlyList<IMigrationStartupCheck> ContributedChecks()
    {
        ChecksRequested++;
        return Checks;
    }

    public Task RecordHostOpened(CancellationToken cancellationToken = default)
    {
        HostOpened = true;
        return Task.CompletedTask;
    }

    public Task<bool> HasHostOpened(CancellationToken cancellationToken = default) => Task.FromResult(HostOpened);
}
