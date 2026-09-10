namespace ServiceControl.Persistence;

using System;

/// <summary>The result of a <see cref="IRetentionSweeper.TryStartManualSweep"/> call.</summary>
public sealed record ManualSweepAttempt(
    RetentionSweepStatus Outcome,
    DateTime? StartedAt,
    DateTime? ErrorCutoff,
    DateTime? EventsCutoff);