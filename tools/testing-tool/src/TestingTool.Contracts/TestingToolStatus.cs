namespace TestingTool.Contracts;

/// <summary>
/// Live snapshot of testing-tool counters, returned by <c>GET /api/status</c> and used as the
/// container liveness/readiness probe target.
/// </summary>
public sealed class TestingToolStatus
{
    /// <summary>Whether the tool is ready to accept scenario control requests.</summary>
    public bool Ready { get; init; }

    /// <summary>Total errors emitted since process start (handler + bypass paths).</summary>
    public long ErrorsSent { get; init; }

    /// <summary>Total error groups replayed since process start.</summary>
    public long ErrorsReplayed { get; init; }

    /// <summary>Total ServiceControl searches executed since process start.</summary>
    public long SearchesExecuted { get; init; }

    /// <summary>The shard id this replica owns, used for disjoint scenario slices when scaled out.</summary>
    public string? ShardId { get; init; }

    /// <summary>Number of scenarios currently running.</summary>
    public int ActiveScenarios { get; init; }

    /// <summary>Aggregate current emission rate across all running scenarios (msgs/sec).</summary>
    public double CurrentRate { get; init; }

    /// <summary>Whether the background replay job is enabled.</summary>
    public bool ReplayEnabled { get; init; }

    /// <summary>Whether the background search job is enabled.</summary>
    public bool SearchEnabled { get; init; }

    /// <summary>ServiceControl API URL the tool is targeting.</summary>
    public string? ServiceControlUrl { get; init; }

    /// <summary>Uptime since process start (formatted string).</summary>
    public string? Uptime { get; init; }
}