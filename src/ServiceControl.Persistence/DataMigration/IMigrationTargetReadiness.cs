namespace ServiceControl.Persistence.DataMigration;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// The target's own startup checks, plus a marker kept in the target database saying a host has already started on it.
/// </summary>
public interface IMigrationTargetReadiness
{
    /// <summary>
    /// The checks this target wants run before the copy starts, in the order they must run.
    /// </summary>
    IReadOnlyList<IMigrationStartupCheck> ContributedChecks();

    /// <summary>
    /// Stamps the marker the first time a host opens on the target, and leaves that first stamp alone on every start after it.
    /// </summary>
    Task RecordHostOpened(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a host has already opened on the target, which is what makes discarding a partial copy a loss rather than a clean abort.
    /// </summary>
    Task<bool> HasHostOpened(CancellationToken cancellationToken = default);
}
