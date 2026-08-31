namespace TestingTool.Contracts;

/// <summary>
/// Request body for <c>POST /api/bypass/start</c>.
/// Starts the direct error-queue bypass writer, which writes failed-message envelopes
/// directly to the ServiceControl error queue without going through a handler.
/// </summary>
public sealed class StartBypassRequest
{
    /// <summary>The scenario whose failure shape to simulate (determines exception type, message, and grouping).</summary>
    public string? Scenario { get; init; }

    /// <summary>Target emission rate in messages/second. Defaults to 100.</summary>
    public double? Rate { get; init; }

    /// <summary>Optional auto-stop duration in seconds. Null/0 = run until explicitly stopped.</summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// Number of parallel worker tasks to use for sending. Each task runs its own timer at
    /// <c>rate / parallelism</c> msg/s so the aggregate approaches the target rate. Defaults to
    /// <see cref="Environment.ProcessorCount"/> when omitted. Increase this if the bypass is
    /// not hitting the target rate due to send latency on a single thread.
    /// </summary>
    public int? Parallelism { get; init; }
}