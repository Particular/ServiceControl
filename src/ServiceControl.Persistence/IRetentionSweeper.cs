namespace ServiceControl.Persistence;

using System;
using System.Threading;

/// <summary>
/// A persister-agnostic retention sweep operation. Only persisters that actually scan and
/// delete aged rows register this interface (e.g. the EFCore SQL persisters). RavenDB does
/// not — its retention is the server-side <c>@expires</c> bundle stamped per-document at
/// write time — so the interface is resolved <em>optionally</em> by the API, which returns
/// <c>501 Not Implemented</c> when no registration is present.
/// </summary>
public interface IRetentionSweeper
{
    /// <summary>
    /// Starts a full retention sweep on a background task
    /// </summary>
    /// <returns>A snapshot describing the run that was started; will respond with a status of AlreadyRunning if there is already a sweep running.</returns>
    ManualSweepAttempt TryStartManualSweep(DateTime? errorCutoff, DateTime? eventsCutoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// A point-in-time snapshot of current sweep execution state for status polling.
    /// </summary>
    RetentionSweepCurrentStatus GetStatus();
}