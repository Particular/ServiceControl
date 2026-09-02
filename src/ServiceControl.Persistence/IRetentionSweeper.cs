namespace ServiceControl.Persistence;

using System;
using System.Threading;
using System.Threading.Tasks;

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
    /// The retention periods and minimum-age rules in force for this instance.
    /// </summary>
    RetentionSweepConfig Config { get; }

    /// <summary>
    /// Starts a full retention sweep on a background task tied to the host lifetime (not the
    /// caller's request token), using the caller-supplied cutoffs. When a cutoff is
    /// <c>null</c> the corresponding sub-sweep derives its cutoff from the configured
    /// retention period as the scheduled path does.
    /// </summary>
    /// <returns>A snapshot describing the run that was started; never throws for "already running"
    /// — that is reported in the returned status.</returns>
    ManualSweepAttempt TryStartManualSweep(DateTime? errorCutoff, DateTime? eventsCutoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// A point-in-time snapshot of sweep execution state for status polling.
    /// </summary>
    RetentionSweepStatus GetStatus();
}

/// <summary>Configuration describing the retention rules in force.</summary>
public sealed record RetentionSweepConfig(TimeSpan ErrorRetentionPeriod, TimeSpan EventsRetentionPeriod);

/// <summary>The outcome of a manual sweep start request.</summary>
public enum ManualSweepOutcome
{
    /// <summary>The sweep was started on a background task.</summary>
    Started,
    /// <summary>A sweep is already running; the caller should poll <see cref="GetStatus"/>.</summary>
    AlreadyRunning
}

/// <summary>The result of a <see cref="IRetentionSweeper.TryStartManualSweep"/> call.</summary>
public sealed record ManualSweepAttempt(
    ManualSweepOutcome Outcome,
    DateTime? StartedAt,
    DateTime? ErrorCutoff,
    DateTime? EventsCutoff);

/// <summary>A point-in-time snapshot of sweep execution state.</summary>
public sealed record RetentionSweepStatus(
    bool IsRunning,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    DateTime? LastErrorCutoff,
    DateTime? LastEventsCutoff,
    string? LastError);