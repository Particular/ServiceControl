namespace ServiceControl.Persistence;

using System;

/// <summary>A point-in-time snapshot of sweep execution state.</summary>
public sealed record RetentionSweepCurrentStatus(
    bool IsRunning,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    DateTime? LastErrorCutoff,
    DateTime? LastEventsCutoff,
    string? LastError);