namespace TestingTool.Contracts;

/// <summary>
/// Request body for <c>POST /api/audit-bypass/start</c>. Starts the direct audit-queue writer,
/// which writes audit envelopes straight to the ServiceControl.Audit instance's audit queue
/// without a real endpoint ever having processed a message.
/// </summary>
public sealed class StartAuditBypassRequest
{
    /// <summary>Target claim rate in claims/second. Defaults to 100.</summary>
    public double? Rate { get; init; }

    /// <summary>Optional auto-stop duration in seconds. Null/0 = run until explicitly stopped.</summary>
    public double? DurationSeconds { get; init; }

    /// <summary>Parallel worker tasks drawing from the run's shared send budget. Defaults to the processor count.</summary>
    public int? Parallelism { get; init; }

    /// <summary>
    /// Saga snapshots to emit per processed message. The default of 0.2 emits two saga snapshots
    /// for every ten processed messages. 0 disables saga episodes entirely.
    /// </summary>
    public double? SagaSnapshotRatio { get; init; }
}
