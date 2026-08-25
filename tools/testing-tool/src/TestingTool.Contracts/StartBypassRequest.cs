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
}