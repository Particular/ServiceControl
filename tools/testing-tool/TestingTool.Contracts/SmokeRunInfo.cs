using System.Text.Json.Serialization;

namespace TestingTool.Contracts;

/// <summary>
/// State of the smoke-test checklist returned by <c>GET /api/smoke</c>: the current run, or the last
/// finished one, or the idle checklist before the first run.
/// </summary>
public sealed class SmokeRunInfo
{
    /// <summary>The identifier that scopes every message, custom check and endpoint of the run. Null before the first run.</summary>
    public string? RunId { get; init; }

    public bool Running { get; init; }

    public string? StartedAt { get; init; }

    public string? FinishedAt { get; init; }

    public required IReadOnlyList<SmokeCheckInfo> Checks { get; init; }
}

public sealed class SmokeCheckInfo
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    /// <summary>What the check does and which ServiceControl API effect it verifies, or for a manual row what a person must verify.</summary>
    public required string Description { get; init; }

    public SmokeCheckStatus Status { get; init; }

    /// <summary>What the check observed. For a failed check, the reason and the last observed state.</summary>
    public string? Detail { get; init; }

    public double? DurationSeconds { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SmokeCheckStatus
{
    Pending,
    Running,
    Passed,
    Failed,
    /// <summary>The check is not automated. A person verifies it.</summary>
    Manual,
    Canceled
}
