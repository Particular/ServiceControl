#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Refuses a copy out of a RavenDB database that a different major version of ServiceControl last wrote. The
/// documents would still load, and the readers would interpret shapes that have since changed, which copies
/// rows that look right and are wrong. Both databases are checked, the primary one and the throughput one.
/// </summary>
sealed class SourceDataVersionIsReadableCheck(RavenReadOnlySourceLifecycle lifecycle) : IMigrationStartupCheck
{
    public string Name => "the source is at a data version this build can read";

    public async Task Run(CancellationToken cancellationToken = default)
    {
        await Verify(lifecycle.Settings.DatabaseName, cancellationToken);
        await Verify(lifecycle.Settings.ThroughputDatabaseName, cancellationToken);
    }

    async Task Verify(string databaseName, CancellationToken cancellationToken)
    {
        using var session = lifecycle.OpenSession(databaseName);
        var stamp = await session.LoadAsync<RavenDataVersion>(RavenDataVersion.DocumentId, cancellationToken);

        if (stamp is null)
        {
            throw new Exception(
                $"The RavenDB database '{databaseName}' carries no ServiceControl data version stamp. Start this instance once on RavenDB with version {RavenDataVersion.Current} before setting {MigrationSettings.EnabledKey}, so the source is brought up to date and stamped.");
        }

        // RavenDB deserializes with Newtonsoft, which ignores the required modifier, so a document saved without
        // the property loads with Version null.
        if (string.IsNullOrWhiteSpace(stamp.Version)
            || !Version.TryParse(stamp.Version.Split('-')[0], out var stamped)
            || !Version.TryParse(RavenDataVersion.Current.Split('-')[0], out var thisBuild))
        {
            throw new Exception(
                $"The RavenDB database '{databaseName}' carries the data version stamp '{stamp.Version}' and this build reports '{RavenDataVersion.Current}', and one of them is not a version this check can compare. It cannot tell whether the source is readable, so it refuses rather than guessing.");
        }

        if (stamped.Major > thisBuild.Major)
        {
            throw new Exception(
                $"The RavenDB database '{databaseName}' was last written by ServiceControl {stamp.Version}, which is newer than this build ({RavenDataVersion.Current}). Migrate with the newer version instead.");
        }

        if (stamped.Major < thisBuild.Major)
        {
            throw new Exception(
                $"The RavenDB database '{databaseName}' was last written by ServiceControl {stamp.Version}, and this build is {RavenDataVersion.Current}. Its documents may be in shapes this build reads differently, which would copy rows that look correct and are wrong. Start this instance once on RavenDB with {RavenDataVersion.Current} before setting {MigrationSettings.EnabledKey}, which brings the source up to date and restamps it.");
        }
    }
}
