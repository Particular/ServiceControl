namespace TestingTool.Contracts;

/// <summary>
/// Live status of the direct error-queue bypass writer, returned by
/// <c>GET /api/bypass/status</c> and included in the <c>GET /api/status</c> snapshot.
/// </summary>
public sealed class BypassStatus
{
    /// <summary>Whether the bypass writer is currently emitting failed-message envelopes.</summary>
    public bool Running { get; init; }

    /// <summary>The scenario whose failure shape is being simulated, or null if idle.</summary>
    public string? Scenario { get; init; }

    /// <summary>Current target emission rate in messages/second (0 if idle).</summary>
    public double Rate { get; init; }

    /// <summary>Total failed-message envelopes written directly to the error queue since process start.</summary>
    public long ErrorsWritten { get; init; }

    /// <summary>When the current bypass run started (UTC ISO 8601, null if idle).</summary>
    public string? StartedAt { get; init; }
}