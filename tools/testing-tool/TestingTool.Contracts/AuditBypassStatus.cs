namespace TestingTool.Contracts;

/// <summary>
/// Live status of the direct audit-queue writer, returned by <c>GET /api/audit-bypass/status</c>
/// and included in the <c>GET /api/status</c> snapshot.
/// </summary>
public sealed class AuditBypassStatus
{
    /// <summary>Whether the writer is currently emitting audit envelopes.</summary>
    public bool Running { get; init; }

    /// <summary>Target claim rate in claims/second (0 if idle). A saga episode claim emits six envelopes.</summary>
    public double Rate { get; init; }

    /// <summary>Envelopes actually written per second since the run started.</summary>
    public double AchievedRate { get; init; }

    /// <summary>Saga snapshots emitted per processed message for the current run.</summary>
    public double SagaSnapshotRatio { get; init; }

    /// <summary>Total processed-message envelopes written to the audit queue since process start.</summary>
    public long ProcessedMessagesWritten { get; init; }

    /// <summary>Total saga-update snapshots written to the audit queue since process start.</summary>
    public long SagaSnapshotsWritten { get; init; }

    /// <summary>Total audit sends that failed since process start.</summary>
    public long MessagesFailed { get; init; }

    /// <summary>When the current run started (UTC ISO 8601, null if idle).</summary>
    public string? StartedAt { get; init; }
}
