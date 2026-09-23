namespace ServiceControl.Persistence;

using System;

/// <summary>
/// A point-in-time snapshot of sweep execution state. It describes the most recent sweep, whether
/// the hourly timer or a manual request started it.
/// </summary>
public sealed record RetentionSweepCurrentStatus(
    bool IsRunning,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    DateTime? LastErrorCutoff,
    DateTime? LastEventsCutoff,
    RetentionSweepOutcome? LastOutcome,
    string? LastError);