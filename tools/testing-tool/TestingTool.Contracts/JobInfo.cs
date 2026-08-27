namespace TestingTool.Contracts;

/// <summary>
/// Describes a recoverability/search job that can be started and stopped from the web UI.
/// Returned by <c>GET /api/jobs</c> and rendered in the "Recoverability Jobs" section.
/// </summary>
public sealed class JobInfo
{
    /// <summary>The stable, url-safe job name used in API paths.</summary>
    public required string Name { get; init; }

    /// <summary>A short human-readable description of what the job does each cycle.</summary>
    public required string Description { get; init; }

    /// <summary>Human-readable category for grouping in the UI (e.g. "Recoverability", "Search").</summary>
    public required string Category { get; init; }

    /// <summary>Whether the job is currently running its periodic cycle.</summary>
    public bool Running { get; init; }

    /// <summary>Configured cycle interval in seconds (0 if idle).</summary>
    public int IntervalSeconds { get; init; }

    /// <summary>Default cycle interval in seconds.</summary>
    public int DefaultIntervalSeconds { get; init; }

    /// <summary>Number of cycles completed since the job was started.</summary>
    public long Cycles { get; init; }

    /// <summary>Number of items processed (groups retried/archived, searches run) since start.</summary>
    public long ItemsProcessed { get; init; }

    /// <summary>When the current run started (UTC ISO 8601, null if idle).</summary>
    public string? StartedAt { get; init; }
}